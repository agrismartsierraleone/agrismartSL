namespace AgrismartAPI.Application.Abstractions;

/// <summary>Thin projection of a Monime checkout session — just what this API needs.</summary>
public record MonimeSessionInfo(
    string MonimeSessionId,
    string Status,
    string? RedirectUrl,
    string? CancelUrl,
    string? SuccessUrl,
    DateTimeOffset? ExpireTime);

/// <summary>The successful, unwrapped result of a Monime checkout-session call, plus the raw wire text for auditing.</summary>
public record MonimeSessionCallResult(MonimeSessionInfo Session, string RequestJson, string ResponseJson, int StatusCode);

/// <summary>
/// Pluggable gateway to Monime's hosted checkout-session API
/// (https://docs.monime.io/apis/versions/caph-2025-08-23). Throws <see cref="Common.MonimeApiException"/>
/// on any failure (Monime-rejected or network) — callers are responsible for persistence/auditing.
/// </summary>
public interface IMonimeCheckoutClient
{
    Task<MonimeSessionCallResult> CreateSessionAsync(
        string name,
        IReadOnlyList<(string Name, long MinorUnitsPrice, int Quantity, string? Reference)> lineItems,
        string? reference,
        string? callbackState,
        string? successUrl,
        string? cancelUrl,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>Fetches the current status of a previously created session — used for reconciliation.</summary>
    Task<MonimeSessionCallResult> GetSessionAsync(string monimeSessionId, CancellationToken cancellationToken = default);
}
