using AgrismartAPI.Domain.Diagnoses;

namespace AgrismartAPI.Application.Abstractions;

/// <summary>Pluggable image-analysis backend. Swap the registered implementation to move from a mock to a real model.</summary>
public interface IDiagnosisEngine
{
    /// <summary>
    /// <paramref name="latitude"/>/<paramref name="longitude"/> are optional (both must be supplied
    /// together or omitted together) and let region-aware backends like Kindwise crop.health weight
    /// suggestions by which crops/pests are actually plausible at that location.
    /// </summary>
    Task<DiagnosisResult> DiagnoseAsync(
        Stream imageStream,
        string fileName,
        double? latitude = null,
        double? longitude = null,
        CancellationToken cancellationToken = default);
}
