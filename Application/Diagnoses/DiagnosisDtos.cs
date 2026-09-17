using AgrismartAPI.Domain.Diagnoses;

namespace AgrismartAPI.Application.Diagnoses;

public record TreatmentStepDto(int Order, string Title, string Description);

public record DiagnosisResultDto(
    string PlantName,
    string? DiseaseName,
    double Confidence,
    Severity Severity,
    string Summary,
    IReadOnlyList<TreatmentStepDto> TreatmentSteps);
