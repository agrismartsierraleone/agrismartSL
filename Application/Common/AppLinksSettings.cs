namespace AgrismartAPI.Application.Common;

public class AppLinksSettings
{
    public const string SectionName = "AppLinks";

    /// <summary>Web login URL, shown to Sellers/Admins (they use the admin web app).</summary>
    public string LoginUrl { get; set; } = string.Empty;

    /// <summary>Google Play listing, shown to Buyers (they use the mobile app).</summary>
    public string PlayStoreUrl { get; set; } = string.Empty;

    /// <summary>Apple App Store listing, shown to Buyers (they use the mobile app).</summary>
    public string AppStoreUrl { get; set; } = string.Empty;
}
