using System.Text.Json.Serialization;

namespace AgrismartAPI.Infrastructure.Diagnosis.Kindwise;

/// <summary>Body for POST {BaseUrl}/api/v1/identification. latitude/longitude/similar_images are omitted from
/// the JSON entirely when not supplied, rather than sent as null/false — Kindwise's crop.health API rejects an
/// explicit "similar_images": false with a 400 ("Unknown modifier"); it only accepts the field when true.</summary>
public record KindwiseIdentificationRequest(
    [property: JsonPropertyName("images")] string[] Images,
    [property: JsonPropertyName("similar_images"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? SimilarImages = null,
    [property: JsonPropertyName("latitude"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Latitude = null,
    [property: JsonPropertyName("longitude"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Longitude = null);
