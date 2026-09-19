using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Infrastructure.Payments.Monime;

/// <summary>
/// Talks to Monime's hosted checkout-session and payment-code APIs
/// (https://docs.monime.io/apis/versions/caph-2025-08-23). Unwraps the {success, messages, result}
/// envelope and throws <see cref="MonimeApiException"/> on any failure (Monime-rejected or network) —
/// callers are responsible for persistence/auditing.
/// </summary>
public class MonimeCheckoutClient(HttpClient httpClient, IOptions<MonimeSettings> options) : IMonimeCheckoutClient
{
    private static readonly JsonSerializerOptions WireJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly MonimeSettings _settings = options.Value;

    public async Task<MonimeSessionCallResult> CreateSessionAsync(
        string name,
        IReadOnlyList<(string Name, long MinorUnitsPrice, int Quantity, string? Reference)> lineItems,
        string? reference,
        string? callbackState,
        string? successUrl,
        string? cancelUrl,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var body = new MonimeCreateCheckoutSessionBody
        {
            Name = name,
            LineItems = lineItems
                .Select(item => new MonimeLineItem
                {
                    Name = item.Name,
                    Price = new MonimePrice { Currency = _settings.Currency, Value = item.MinorUnitsPrice },
                    Quantity = item.Quantity,
                    Reference = item.Reference,
                })
                .ToList(),
            Reference = reference,
            CallbackState = callbackState,
            SuccessUrl = string.IsNullOrWhiteSpace(successUrl) ? NullIfEmpty(_settings.DefaultSuccessUrl) : successUrl,
            CancelUrl = string.IsNullOrWhiteSpace(cancelUrl) ? NullIfEmpty(_settings.DefaultCancelUrl) : cancelUrl,
        };

        var requestJson = JsonSerializer.Serialize(body, WireJsonOptions);
        var (httpResponse, responseBody) = await PostAsync("/v1/checkout-sessions", requestJson, idempotencyKey, cancellationToken);
        var result = Unwrap<MonimeCheckoutSessionResult>(httpResponse, responseBody, requestJson, "checkout-session");

        var session = new MonimeSessionInfo(result.Id, result.Status, result.RedirectUrl, result.CancelUrl, result.SuccessUrl, result.ExpireTime);
        return new MonimeSessionCallResult(session, requestJson, responseBody, (int)httpResponse.StatusCode);
    }

    public async Task<MonimeSessionCallResult> GetSessionAsync(string monimeSessionId, CancellationToken cancellationToken = default)
    {
        var (httpResponse, responseBody) = await GetAsync($"/v1/checkout-sessions/{monimeSessionId}", cancellationToken);
        var result = Unwrap<MonimeCheckoutSessionResult>(httpResponse, responseBody, requestJson: null, "checkout-session");

        var session = new MonimeSessionInfo(result.Id, result.Status, result.RedirectUrl, result.CancelUrl, result.SuccessUrl, result.ExpireTime);
        return new MonimeSessionCallResult(session, string.Empty, responseBody, (int)httpResponse.StatusCode);
    }

    public Task CancelSessionAsync(string monimeSessionId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"/v1/checkout-sessions/{monimeSessionId}", cancellationToken);

    public async Task<MonimePaymentCodeCallResult> CreatePaymentCodeAsync(
        string name, long minorUnitsAmount, string duration, string? reference, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        var body = new MonimeCreatePaymentCodeBody
        {
            Name = name,
            Amount = new MonimePrice { Currency = _settings.Currency, Value = minorUnitsAmount },
            Duration = duration,
            Reference = reference,
        };

        var requestJson = JsonSerializer.Serialize(body, WireJsonOptions);
        var (httpResponse, responseBody) = await PostAsync("/v1/payment-codes", requestJson, idempotencyKey, cancellationToken);
        var result = Unwrap<MonimePaymentCodeResult>(httpResponse, responseBody, requestJson, "payment code");

        var paymentCode = new MonimePaymentCodeInfo(result.Id, result.Status, result.UssdCode, result.ExpireTime);
        return new MonimePaymentCodeCallResult(paymentCode, requestJson, responseBody, (int)httpResponse.StatusCode);
    }

    public async Task<MonimePaymentCodeCallResult> GetPaymentCodeAsync(string paymentCodeId, CancellationToken cancellationToken = default)
    {
        var (httpResponse, responseBody) = await GetAsync($"/v1/payment-codes/{paymentCodeId}", cancellationToken);
        var result = Unwrap<MonimePaymentCodeResult>(httpResponse, responseBody, requestJson: null, "payment code");

        var paymentCode = new MonimePaymentCodeInfo(result.Id, result.Status, result.UssdCode, result.ExpireTime);
        return new MonimePaymentCodeCallResult(paymentCode, string.Empty, responseBody, (int)httpResponse.StatusCode);
    }

    public Task CancelPaymentCodeAsync(string paymentCodeId, CancellationToken cancellationToken = default) =>
        DeleteAsync($"/v1/payment-codes/{paymentCodeId}", cancellationToken);

    private async Task<(HttpResponseMessage Response, string Body)> PostAsync(string path, string requestJson, string idempotencyKey, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl.TrimEnd('/')}{path}")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
        };
        ApplyHeaders(request, idempotencyKey);
        return await SendAsync(request, requestJson, cancellationToken);
    }

    private async Task<(HttpResponseMessage Response, string Body)> GetAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl.TrimEnd('/')}{path}");
        ApplyHeaders(request, idempotencyKey: null);
        return await SendAsync(request, requestJson: null, cancellationToken);
    }

    private async Task DeleteAsync(string path, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{_settings.BaseUrl.TrimEnd('/')}{path}");
        ApplyHeaders(request, idempotencyKey: null);
        var (httpResponse, responseBody) = await SendAsync(request, requestJson: null, cancellationToken);

        if (!httpResponse.IsSuccessStatusCode)
        {
            var errorMessage = ExtractMessages(TryParse(responseBody)) ?? "Monime rejected the cancel request.";
            throw new MonimeApiException(errorMessage, (int)httpResponse.StatusCode, requestJson: null, responseBody);
        }
    }

    private void ApplyHeaders(HttpRequestMessage request, string? idempotencyKey)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _settings.AuthorizationToken);
        request.Headers.Add("Monime-Space-Id", _settings.SpaceId);
        request.Headers.Add("Monime-Version", _settings.Version);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }
    }

    private async Task<(HttpResponseMessage Response, string Body)> SendAsync(HttpRequestMessage request, string? requestJson, CancellationToken cancellationToken)
    {
        try
        {
            var response = await httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response, body);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new MonimeApiException($"Could not reach the payment provider: {ex.Message}", statusCode: null, requestJson: requestJson, responseJson: null);
        }
    }

    private static T Unwrap<T>(HttpResponseMessage httpResponse, string responseBody, string? requestJson, string resourceName)
    {
        var responseDoc = TryParse(responseBody);
        using var _ = responseDoc;

        JsonElement? resultElement = responseDoc is not null
            && responseDoc.RootElement.TryGetProperty("result", out var result)
            && result.ValueKind == JsonValueKind.Object
                ? result
                : null;

        if (!httpResponse.IsSuccessStatusCode || resultElement is null)
        {
            var errorMessage = ExtractMessages(responseDoc) ?? $"Monime rejected the {resourceName} request.";
            throw new MonimeApiException(errorMessage, (int)httpResponse.StatusCode, requestJson, responseBody);
        }

        return resultElement.Value.Deserialize<T>(WireJsonOptions)
               ?? throw new MonimeApiException($"Monime returned an empty {resourceName} result.", (int)httpResponse.StatusCode, requestJson, responseBody);
    }

    private static JsonDocument? TryParse(string json)
    {
        try
        {
            return JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? ExtractMessages(JsonDocument? doc)
    {
        if (doc is null || !doc.RootElement.TryGetProperty("messages", out var messages) || messages.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var texts = messages.EnumerateArray()
            .Where(m => m.ValueKind == JsonValueKind.String)
            .Select(m => m.GetString())
            .Where(text => !string.IsNullOrWhiteSpace(text));

        var joined = string.Join(" ", texts);
        return string.IsNullOrWhiteSpace(joined) ? null : joined;
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}
