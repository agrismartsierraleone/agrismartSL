using AgrismartAPI.Application.Diagnoses;

namespace AgrismartAPI.Application.Scans;

public record PlantScanDto(
    Guid Id,
    string ImagePath,
    DiagnosisResultDto Diagnosis,
    DateTimeOffset ScannedAt);
