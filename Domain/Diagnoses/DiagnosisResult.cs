namespace AgrismartAPI.Domain.Diagnoses;

public class DiagnosisResult
{
    public string PlantName { get; private set; }
    public string? DiseaseName { get; private set; }
    public double Confidence { get; private set; }
    public Severity Severity { get; private set; }
    public string Summary { get; private set; }
    public IReadOnlyList<TreatmentStep> TreatmentSteps { get; private set; }

    private DiagnosisResult(
        string plantName,
        string? diseaseName,
        double confidence,
        Severity severity,
        string summary,
        IReadOnlyList<TreatmentStep> treatmentSteps)
    {
        PlantName = plantName;
        DiseaseName = diseaseName;
        Confidence = confidence;
        Severity = severity;
        Summary = summary;
        TreatmentSteps = treatmentSteps;
    }

    // EF Core
    private DiagnosisResult()
    {
        PlantName = null!;
        Summary = null!;
        TreatmentSteps = new List<TreatmentStep>();
    }

    public static DiagnosisResult Create(
        string plantName,
        string? diseaseName,
        double confidence,
        Severity severity,
        string summary,
        IReadOnlyList<TreatmentStep> treatmentSteps)
    {
        if (string.IsNullOrWhiteSpace(plantName))
        {
            throw new ArgumentException("Plant name is required.", nameof(plantName));
        }

        if (confidence is < 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0.0 and 1.0.");
        }

        if (severity != Severity.Healthy && string.IsNullOrWhiteSpace(diseaseName))
        {
            throw new ArgumentException("Disease name is required unless severity is Healthy.", nameof(diseaseName));
        }

        return new DiagnosisResult(
            plantName.Trim(),
            severity == Severity.Healthy ? null : diseaseName!.Trim(),
            confidence,
            severity,
            summary,
            treatmentSteps);
    }
}
