namespace AgrismartAPI.Application.Common;

/// <summary>Options for Monime's hosted checkout-session API (https://docs.monime.io/apis/versions/caph-2025-08-23).</summary>
public class MonimeSettings
{
    public const string SectionName = "Monime";

    public string BaseUrl { get; set; } = "https://api.monime.io";

    /// <summary>Space-scoped bearer token from the Monime dashboard. Never accepted from a client request.</summary>
    public string AuthorizationToken { get; set; } = string.Empty;

    /// <summary>Which Monime "space" (merchant account) to act as, e.g. "spc-...".</summary>
    public string SpaceId { get; set; } = string.Empty;

    /// <summary>Pinned API version sent as the Monime-Version header, e.g. "caph.2025-08-23".</summary>
    public string Version { get; set; } = "caph.2025-08-23";

    /// <summary>3-letter ISO currency code used for all line items, e.g. "SLE".</summary>
    public string Currency { get; set; } = "SLE";

    /// <summary>Used when the client doesn't send its own successUrl.</summary>
    public string DefaultSuccessUrl { get; set; } = string.Empty;

    /// <summary>Used when the client doesn't send its own cancelUrl.</summary>
    public string DefaultCancelUrl { get; set; } = string.Empty;
}
