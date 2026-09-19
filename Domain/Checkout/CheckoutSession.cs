using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Checkout;

/// <summary>A Monime hosted-checkout session created for one <c>Order</c> (see MONIME-INTEGRATION-README.md).</summary>
public class CheckoutSession : Entity
{
    public Guid OrderId { get; private init; }
    public Guid UserId { get; private init; }
    public string MonimeSessionId { get; private init; }

    /// <summary>Monime's own session status — one of "pending", "completed", "cancelled", "expired".</summary>
    public string Status { get; private set; }

    public string? RedirectUrl { get; private init; }
    public string? CancelUrl { get; private init; }
    public string? SuccessUrl { get; private init; }
    public DateTimeOffset? ExpireTime { get; private init; }

    public string IdempotencyKey { get; private init; }

    /// <summary>The raw Monime response body, kept verbatim for support/troubleshooting.</summary>
    public string RawResponseJson { get; private set; }

    /// <summary>
    /// Monime's own payment-code id (e.g. "pmc-..."), created alongside the session so the customer
    /// has a dial-a-USSD-code fallback to the hosted redirect page. Null if that create call failed —
    /// non-fatal, the redirect flow still works. See MONIME-INTEGRATION-README.md §2.
    /// </summary>
    public string? PaymentCodeId { get; private set; }

    /// <summary>The actual, per-transaction USSD dial string (e.g. "*715*18284#") a customer can dial to pay. Not static.</summary>
    public string? UssdCode { get; private set; }

    /// <summary>Monime's payment-code status — one of "pending", "processing", "completed", "expired", "cancelled".</summary>
    public string? PaymentCodeStatus { get; private set; }

    public DateTimeOffset? PaymentCodeExpireTime { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset? LastSyncedAt { get; private set; }

    private CheckoutSession(
        Guid orderId,
        Guid userId,
        string monimeSessionId,
        string status,
        string? redirectUrl,
        string? cancelUrl,
        string? successUrl,
        DateTimeOffset? expireTime,
        string idempotencyKey,
        string rawResponseJson)
    {
        OrderId = orderId;
        UserId = userId;
        MonimeSessionId = monimeSessionId;
        Status = status;
        RedirectUrl = redirectUrl;
        CancelUrl = cancelUrl;
        SuccessUrl = successUrl;
        ExpireTime = expireTime;
        IdempotencyKey = idempotencyKey;
        RawResponseJson = rawResponseJson;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private CheckoutSession()
    {
        MonimeSessionId = null!;
        Status = null!;
        IdempotencyKey = null!;
        RawResponseJson = null!;
    }

    public static CheckoutSession Create(
        Guid orderId,
        Guid userId,
        string monimeSessionId,
        string status,
        string? redirectUrl,
        string? cancelUrl,
        string? successUrl,
        DateTimeOffset? expireTime,
        string idempotencyKey,
        string rawResponseJson)
    {
        if (string.IsNullOrWhiteSpace(monimeSessionId))
        {
            throw new ArgumentException("Monime session id is required.", nameof(monimeSessionId));
        }

        return new CheckoutSession(orderId, userId, monimeSessionId, status, redirectUrl, cancelUrl, successUrl, expireTime, idempotencyKey, rawResponseJson);
    }

    /// <summary>Applies the latest status/raw payload fetched from Monime during reconciliation.</summary>
    public void UpdateStatus(string status, string rawResponseJson)
    {
        Status = status;
        RawResponseJson = rawResponseJson;
        LastSyncedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Records the payment code Monime created alongside this session. Safe to skip on failure — the redirect flow still works.</summary>
    public void AttachPaymentCode(string paymentCodeId, string? ussdCode, string status, DateTimeOffset? expireTime)
    {
        PaymentCodeId = paymentCodeId;
        UssdCode = ussdCode;
        PaymentCodeStatus = status;
        PaymentCodeExpireTime = expireTime;
    }

    public void UpdatePaymentCodeStatus(string status) => PaymentCodeStatus = status;
}
