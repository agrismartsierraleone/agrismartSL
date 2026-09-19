namespace AgrismartAPI.Infrastructure.Payments.Monime;

// Wire-format models matching Monime's JSON exactly (camelCase on the wire — see WireJsonOptions in
// MonimeCheckoutClient). Kept internal and separate from this API's own public DTOs so this API's
// request/response shape can evolve independently of Monime's.

internal class MonimeCreateCheckoutSessionBody
{
    public required string Name { get; set; }
    public required List<MonimeLineItem> LineItems { get; set; }
    public string? Description { get; set; }
    public string? CancelUrl { get; set; }
    public string? SuccessUrl { get; set; }
    public string? CallbackState { get; set; }
    public string? Reference { get; set; }
    public string? FinancialAccountId { get; set; }
    public MonimePaymentOptions? PaymentOptions { get; set; }
    public Dictionary<string, string>? Metadata { get; set; }
}

internal class MonimeLineItem
{
    public required string Name { get; set; }
    public required MonimePrice Price { get; set; }
    public string Type { get; set; } = "custom";
    public int Quantity { get; set; } = 1;
    public string? Reference { get; set; }
}

internal class MonimePrice
{
    public required string Currency { get; set; }

    /// <summary>Whole minor units (e.g. cents) — never a float.</summary>
    public required long Value { get; set; }
}

internal class MonimePaymentOptions
{
    public MonimePaymentMethodOptions? Card { get; set; }
    public MonimePaymentMethodOptions? Bank { get; set; }
    public MonimePaymentMethodOptions? Momo { get; set; }
    public MonimePaymentMethodOptions? Wallet { get; set; }
}

internal class MonimePaymentMethodOptions
{
    public bool? Disable { get; set; }
    public List<string>? EnabledProviders { get; set; }
    public List<string>? DisabledProviders { get; set; }
}

/// <summary>Every Monime response is wrapped in this envelope, success or error.</summary>
internal class MonimeEnvelope<T>
{
    public bool Success { get; set; }
    public List<string>? Messages { get; set; }
    public T? Result { get; set; }
}

internal class MonimeCheckoutSessionResult
{
    public required string Id { get; set; }

    /// <summary>"pending" | "completed" | "cancelled" | "expired".</summary>
    public required string Status { get; set; }

    public string? Name { get; set; }
    public string? OrderNumber { get; set; }
    public string? Reference { get; set; }
    public string? RedirectUrl { get; set; }
    public string? CancelUrl { get; set; }
    public string? SuccessUrl { get; set; }
    public DateTimeOffset? ExpireTime { get; set; }
    public DateTimeOffset? CreateTime { get; set; }
}

// Payment codes (https://docs.monime.io/apis/versions/caph-2025-08-23/payment-code/object.md) are a
// separate Monime resource: a dynamic, per-transaction USSD dial string the customer can use instead
// of (or alongside) the hosted checkout-session redirect. The "18284" in an example like
// "*715*18284#" is generated per payment code — there is no static merchant USSD code.

internal class MonimeCreatePaymentCodeBody
{
    public required string Name { get; set; }
    public string Mode { get; set; } = "one_time";
    public required MonimePrice Amount { get; set; }

    /// <summary>Go duration string, e.g. "1h". Monime's own default is "10m" — too short to match a ~1h checkout-session window.</summary>
    public string? Duration { get; set; }

    public string? Reference { get; set; }
}

internal class MonimePaymentCodeResult
{
    public required string Id { get; set; }

    /// <summary>"pending" | "processing" | "completed" | "expired" | "cancelled".</summary>
    public required string Status { get; set; }

    public string? Name { get; set; }
    public string? UssdCode { get; set; }
    public string? Reference { get; set; }
    public DateTimeOffset? ExpireTime { get; set; }
    public DateTimeOffset? CreateTime { get; set; }
}
