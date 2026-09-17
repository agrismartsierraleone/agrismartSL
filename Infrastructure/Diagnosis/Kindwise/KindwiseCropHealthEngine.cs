using System.Text;
using System.Text.Json;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Diagnoses;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Infrastructure.Diagnosis.Kindwise;

/// <summary>
/// Real IDiagnosisEngine backed by Kindwise's crop.health API (https://crop.kindwise.com/docs), which
/// identifies crops together with the pests and diseases affecting them from a single photo.
/// Registered only when "Kindwise:ApiKey" is configured — see Infrastructure/DependencyInjection.cs.
/// </summary>
public class KindwiseCropHealthEngine(HttpClient httpClient, IOptions<KindwiseSettings> options) : IDiagnosisEngine
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly KindwiseSettings _settings = options.Value;

    public async Task<DiagnosisResult> DiagnoseAsync(
        Stream imageStream,
        string fileName,
        double? latitude = null,
        double? longitude = null,
        CancellationToken cancellationToken = default)
    {
        var base64Image = await ToBase64Async(imageStream, cancellationToken);

        // latitude/longitude let Kindwise weight suggestions toward crops and pests that are actually
        // plausible at that location, which measurably improves accuracy — see crop.health docs.
        var requestBody = new KindwiseIdentificationRequest([base64Image], Latitude: latitude, Longitude: longitude);

        var requestUri = $"/api/v1/identification?details={string.Join(',', _settings.Details)}";
        using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
        {
            Content = JsonContent.Create(requestBody, options: JsonOptions)
        };
        request.Headers.Add("Api-Key", _settings.ApiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Kindwise crop.health request failed with {(int)response.StatusCode}: {body}");
        }

        var payload = JsonSerializer.Deserialize<KindwiseIdentificationResponse>(body, JsonOptions)
                      ?? throw new InvalidOperationException("Kindwise crop.health returned an empty response.");

        return MapToDiagnosisResult(payload);
    }

    private static async Task<string> ToBase64Async(Stream imageStream, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);
        return Convert.ToBase64String(buffer.ToArray());
    }

    private static DiagnosisResult MapToDiagnosisResult(KindwiseIdentificationResponse payload)
    {
        var cropSuggestion = payload.Result?.Crop?.Suggestions.FirstOrDefault();
        var isPlant = payload.Result?.IsPlant is null or { Binary: true };
        var plantName = isPlant && cropSuggestion is not null ? Capitalize(cropSuggestion.Name) : "Unrecognized plant";

        var issueSuggestion = payload.Result?.Disease?.Suggestions
            .OrderByDescending(s => s.Probability)
            .FirstOrDefault();

        // Kindwise treats a low-confidence top suggestion as "nothing conclusive found" rather than a diagnosis.
        if (issueSuggestion is null || issueSuggestion.Probability < 0.15)
        {
            return DiagnosisResult.Create(
                plantName,
                null,
                issueSuggestion?.Probability ?? 1.0,
                Severity.Healthy,
                "No pest or disease was identified with meaningful confidence in this image.",
                [new TreatmentStep(1, "Continue routine monitoring", "Re-scan weekly to catch early symptoms.")]);
        }

        var details = issueSuggestion.Details ?? [];
        var issueType = GetString(details, "type");
        var isPest = string.Equals(issueType, "pest", StringComparison.OrdinalIgnoreCase);

        var diseaseName = Capitalize(issueSuggestion.Name);
        var severity = MapSeverity(GetString(details, "severity"));
        var summary = GetString(details, "description")
                      ?? (isPest
                          ? $"{diseaseName} was detected on the {plantName.ToLowerInvariant()}."
                          : $"Signs consistent with {diseaseName} were found on the {plantName.ToLowerInvariant()}.");

        var treatmentSteps = BuildTreatmentSteps(details);

        return DiagnosisResult.Create(plantName, diseaseName, issueSuggestion.Probability, severity, summary, treatmentSteps);
    }

    private static IReadOnlyList<TreatmentStep> BuildTreatmentSteps(Dictionary<string, JsonElement> details)
    {
        if (!details.TryGetValue("treatment", out var treatment) || treatment.ValueKind != JsonValueKind.Object)
        {
            return [new TreatmentStep(1, "Consult an agronomist", "No structured treatment guidance was returned for this finding.")];
        }

        var steps = new List<TreatmentStep>();
        foreach (var (category, title) in new[] { ("prevention", "Prevention"), ("biological", "Biological control"), ("chemical", "Chemical control") })
        {
            var instructions = ExtractStrings(treatment, category);
            if (instructions.Count > 0)
            {
                steps.Add(new TreatmentStep(steps.Count + 1, title, string.Join(" ", instructions)));
            }
        }

        return steps.Count > 0
            ? steps
            : [new TreatmentStep(1, "Consult an agronomist", "No structured treatment guidance was returned for this finding.")];
    }

    private static List<string> ExtractStrings(JsonElement obj, string propertyName)
    {
        var result = new List<string>();
        if (!obj.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in array.EnumerateArray())
        {
            var text = item.ValueKind switch
            {
                JsonValueKind.String => item.GetString(),
                JsonValueKind.Object when item.TryGetProperty("description", out var d) => d.GetString(),
                _ => null
            };

            if (!string.IsNullOrWhiteSpace(text))
            {
                result.Add(text!);
            }
        }

        return result;
    }

    private static string? GetString(Dictionary<string, JsonElement> details, string key) =>
        details.TryGetValue(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static Severity MapSeverity(string? severity) => severity?.Trim().ToLowerInvariant() switch
    {
        "low" or "mild" => Severity.Low,
        "high" or "severe" or "critical" => Severity.High,
        "moderate" or "medium" => Severity.Moderate,
        _ => Severity.Moderate
    };

    private static string Capitalize(string value) =>
        string.IsNullOrWhiteSpace(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
