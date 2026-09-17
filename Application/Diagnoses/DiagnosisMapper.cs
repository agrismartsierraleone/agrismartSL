using AgrismartAPI.Domain.Diagnoses;

namespace AgrismartAPI.Application.Diagnoses;

public static class DiagnosisMapper
{
    public static DiagnosisResultDto ToDto(DiagnosisResult result) => new(
        result.PlantName,
        result.DiseaseName,
        result.Confidence,
        result.Severity,
        result.Summary,
        result.TreatmentSteps.Select(step => new TreatmentStepDto(step.Order, step.Title, step.Description)).ToList());

    public static DiagnosisResult ToDomain(DiagnosisResultDto dto) => DiagnosisResult.Create(
        dto.PlantName,
        dto.DiseaseName,
        dto.Confidence,
        dto.Severity,
        dto.Summary,
        dto.TreatmentSteps.Select(step => new TreatmentStep(step.Order, step.Title, step.Description)).ToList());
}
