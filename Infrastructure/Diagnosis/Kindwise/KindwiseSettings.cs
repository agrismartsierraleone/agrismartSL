namespace AgrismartAPI.Infrastructure.Diagnosis.Kindwise;

/// <summary>Options for the Kindwise crop.health identification API (https://crop.kindwise.com/docs).</summary>
public class KindwiseSettings
{
    public const string SectionName = "Kindwise";

    /// <summary>Api-Key issued by kindwise.com. When empty, DI falls back to MockDiagnosisEngine so local dev keeps working without an account.</summary>
    public string ApiKey { get; set; } = string.Empty;

    public string BaseUrl { get; set; } = "https://crop.kindwise.com";

    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>"details" query param requested from the identification endpoint.</summary>
    public string[] Details { get; set; } =
    [
        "common_names", "description", "treatment", "severity", "type", "symptoms"
    ];
}
