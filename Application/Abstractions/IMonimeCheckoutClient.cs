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
/// Thin projection of a Monime payment code — a dynamic, per-transaction USSD dial string (e.g.
/// "*715*18284#") the customer can dial instead of following the checkout session's redirect.
/// </summary>
public record MonimePaymentCodeInfo(string PaymentCodeId, string Status, string? UssdCode, DateTimeOffset? ExpireTime);

/// <summary>The successful, unwrapped result of a Monime payment-code call, plus the raw wire text for auditing.</summary>
public record MonimePaymentCodeCallResult(MonimePaymentCodeInfo PaymentCode, string RequestJson, string ResponseJson, int StatusCode);

/// <summary>
/// Pluggable gateway to Monime's hosted checkout-session and payment-code APIs
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

    /// <summary>Fetches the current status of a previously created session — used for admin reconciliation.</summary>
    Task<MonimeSessionCallResult> GetSessionAsync(string monimeSessionId, CancellationToken cancellationToken = default);

    /// <summary>Best-effort cleanup: deletes a session Monime says hasn't been initiated by the user yet. Safe to ignore failures.</summary>
    Task CancelSessionAsync(string monimeSessionId, CancellationToken cancellationToken = default);

    /// <summary>Creates a one-time payment code — the real, per-transaction USSD dial string (see <see cref="MonimePaymentCodeInfo.UssdCode"/>).</summary>
    Task<MonimePaymentCodeCallResult> CreatePaymentCodeAsync(
        string name, long minorUnitsAmount, string duration, string? reference, string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Fetches the current status of a previously created payment code — used for admin reconciliation.</summary>
    Task<MonimePaymentCodeCallResult> GetPaymentCodeAsync(string paymentCodeId, CancellationToken cancellationToken = default);

    /// <summary>Best-effort cleanup: deletes a payment code Monime says hasn't been used yet. Safe to ignore failures.</summary>
    Task CancelPaymentCodeAsync(string paymentCodeId, CancellationToken cancellationToken = default);
}
