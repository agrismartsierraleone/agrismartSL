using System.Text.Json.Serialization;

namespace AgrismartAPI.Infrastructure.Diagnosis.Kindwise;

/// <summary>
/// Shapes for POST {BaseUrl}/api/v1/identification, mirrored from the official
/// kindwise-api-client (flowerchecker/kindwise-api-client, kindwise/async_api/crop_health.py + models.py).
/// </summary>
public class KindwiseIdentificationResponse
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("result")]
    public KindwiseResult? Result { get; set; }
}

public class KindwiseResult
{
    [JsonPropertyName("is_plant")]
    public KindwiseIsPlant? IsPlant { get; set; }

    [JsonPropertyName("crop")]
    public KindwiseClassification? Crop { get; set; }

    [JsonPropertyName("disease")]
    public KindwiseClassification? Disease { get; set; }
}

public class KindwiseIsPlant
{
    [JsonPropertyName("probability")]
    public double Probability { get; set; }

    [JsonPropertyName("binary")]
    public bool Binary { get; set; }
}

public class KindwiseClassification
{
    [JsonPropertyName("suggestions")]
    public List<KindwiseSuggestion> Suggestions { get; set; } = [];
}

public class KindwiseSuggestion
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("probability")]
    public double Probability { get; set; }

    /// <summary>Loose bag — the "details" query param controls which of these keys are populated
    /// (common_names, description, treatment, severity, type, symptoms, ...). Kept as raw JSON
    /// since Kindwise documents the shape but doesn't guarantee it, so parsing stays defensive.</summary>
    [JsonPropertyName("details")]
    public Dictionary<string, System.Text.Json.JsonElement>? Details { get; set; }
}
