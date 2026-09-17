using AgrismartAPI.Domain.Common;
using AgrismartAPI.Domain.Diagnoses;

namespace AgrismartAPI.Domain.Scans;

public class PlantScan : Entity
{
    public Guid UserId { get; private init; }
    public string ImagePath { get; private init; }
    public DiagnosisResult Diagnosis { get; private init; }
    public DateTimeOffset ScannedAt { get; private init; }

    private PlantScan(Guid userId, string imagePath, DiagnosisResult diagnosis)
    {
        UserId = userId;
        ImagePath = imagePath;
        Diagnosis = diagnosis;
        ScannedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private PlantScan()
    {
        ImagePath = null!;
        Diagnosis = null!;
    }

    public static PlantScan Create(Guid userId, string imagePath, DiagnosisResult diagnosis)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            throw new ArgumentException("Image path is required.", nameof(imagePath));
        }

        return new PlantScan(userId, imagePath, diagnosis);
    }
}
