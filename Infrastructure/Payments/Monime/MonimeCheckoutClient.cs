using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Infrastructure.Payments.Monime;

/// <summary>
/// Talks to Monime's hosted checkout-session API (https://docs.monime.io/apis/versions/caph-2025-08-23).
/// Unwraps the {success, messages, result} envelope and throws <see cref="MonimeApiException"/> on any
/// failure (Monime-rejected or network) — callers are responsible for persistence/auditing.
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

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_settings.BaseUrl.TrimEnd('/')}/v1/checkout-sessions")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json"),
        };
        ApplyHeaders(request, idempotencyKey);

        var (httpResponse, responseBody) = await SendAsync(request, requestJson, cancellationToken);

        return Unwrap(httpResponse, responseBody, requestJson);
    }

    public async Task<MonimeSessionCallResult> GetSessionAsync(string monimeSessionId, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{_settings.BaseUrl.TrimEnd('/')}/v1/checkout-sessions/{monimeSessionId}");
        ApplyHeaders(request, idempotencyKey: null);

        var (httpResponse, responseBody) = await SendAsync(request, requestJson: null, cancellationToken);

        return Unwrap(httpResponse, responseBody, requestJson: null);
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

    private static MonimeSessionCallResult Unwrap(HttpResponseMessage httpResponse, string responseBody, string? requestJson)
    {
        JsonDocument? responseDoc;
        try
        {
            responseDoc = JsonDocument.Parse(responseBody);
        }
        catch (JsonException)
        {
            responseDoc = null;
        }

        using var _ = responseDoc;

        JsonElement? resultElement = responseDoc is not null
            && responseDoc.RootElement.TryGetProperty("result", out var result)
            && result.ValueKind == JsonValueKind.Object
                ? result
                : null;

        if (!httpResponse.IsSuccessStatusCode || resultElement is null)
        {
            var errorMessage = ExtractMessages(responseDoc) ?? "Monime rejected the checkout-session request.";
            throw new MonimeApiException(errorMessage, (int)httpResponse.StatusCode, requestJson, responseBody);
        }

        var sessionResult = resultElement.Value.Deserialize<MonimeCheckoutSessionResult>(WireJsonOptions)
                             ?? throw new MonimeApiException("Monime returned an empty session result.", (int)httpResponse.StatusCode, requestJson, responseBody);

        var session = new MonimeSessionInfo(sessionResult.Id, sessionResult.Status, sessionResult.RedirectUrl, sessionResult.CancelUrl, sessionResult.SuccessUrl, sessionResult.ExpireTime);
        return new MonimeSessionCallResult(session, requestJson ?? string.Empty, responseBody, (int)httpResponse.StatusCode);
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
