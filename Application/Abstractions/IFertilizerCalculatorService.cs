using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Fertilizer;

namespace AgrismartAPI.Application.Abstractions;

/// <summary>
/// Pure fertilizer arithmetic: no database or network access, so it can be unit-tested directly.
/// Throws <see cref="Common.ValidationException"/> (with field-level errors) for invalid input.
/// </summary>
public interface IFertilizerCalculatorService
{
    /// <param name="request">The farmer's inputs.</param>
    /// <param name="crop">The crop named by <c>request.CropId</c>.</param>
    /// <param name="catalogue">All known fertilizer products; the farmer's selection and automatic plans draw from these.</param>
    FertilizerCalculationResponseDto Calculate(
        FertilizerCalculationRequest request,
        FertilizerCropProfile crop,
        IReadOnlyList<FertilizerProduct> catalogue);
}
