using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Diagnoses;

namespace AgrismartAPI.Infrastructure.Diagnosis;

/// <summary>Canned results standing in for a real model, mirroring the Flutter app's MockDiagnosisService. Swap with a real IDiagnosisEngine to plug in inference.</summary>
public class MockDiagnosisEngine : IDiagnosisEngine
{
    private static readonly IReadOnlyList<Func<DiagnosisResult>> Samples =
    [
        () => DiagnosisResult.Create(
            "Tomato",
            "Early Blight",
            0.91,
            Severity.Moderate,
            "Dark concentric leaf spots consistent with Alternaria solani.",
            [
                new TreatmentStep(1, "Remove affected leaves", "Prune and destroy infected foliage to slow spread."),
                new TreatmentStep(2, "Apply a copper-based fungicide", "Spray weekly until symptoms subside."),
                new TreatmentStep(3, "Improve airflow", "Space plants to reduce humidity around the canopy.")
            ]),
        () => DiagnosisResult.Create(
            "Maize",
            null,
            0.97,
            Severity.Healthy,
            "No visible signs of pest or disease damage.",
            [
                new TreatmentStep(1, "Continue routine monitoring", "Re-scan weekly to catch early symptoms.")
            ]),
        () => DiagnosisResult.Create(
            "Cassava",
            "Cassava Mosaic Disease",
            0.88,
            Severity.High,
            "Mottled, distorted leaves typical of cassava mosaic virus.",
            [
                new TreatmentStep(1, "Uproot and destroy infected plants", "Prevents whitefly vectors from spreading the virus further."),
                new TreatmentStep(2, "Source certified virus-free cuttings", "Replant with resistant varieties."),
                new TreatmentStep(3, "Control whitefly populations", "Use appropriate insecticide or sticky traps.")
            ]),
        () => DiagnosisResult.Create(
            "Pepper",
            "Bacterial Spot",
            0.79,
            Severity.Low,
            "Small water-soaked spots on leaves, early-stage bacterial infection.",
            [
                new TreatmentStep(1, "Avoid overhead irrigation", "Reduces leaf wetness that spreads bacteria."),
                new TreatmentStep(2, "Apply copper-based bactericide", "Treat at first sign of spotting.")
            ])
    ];

    public Task<DiagnosisResult> DiagnoseAsync(
        Stream imageStream,
        string fileName,
        double? latitude = null,
        double? longitude = null,
        CancellationToken cancellationToken = default)
    {
        var sample = Samples[Random.Shared.Next(Samples.Count)];
        return Task.FromResult(sample());
    }
}
