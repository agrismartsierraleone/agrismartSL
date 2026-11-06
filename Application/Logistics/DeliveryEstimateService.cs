using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Logistics;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Logistics;

/// <summary>
/// "Delivery" configuration section. There are deliberately no default rates: estimates stay switched off
/// until real prices are set, so the app never quotes a made-up cost.
/// </summary>
public class DeliveryOptions
{
    public const string SectionName = "Delivery";

    public decimal? BaseFee { get; set; }

    public decimal? RatePerKm { get; set; }

    public decimal? RatePerKg { get; set; }

    /// <summary>Roads are longer than a straight line between two towns.</summary>
    public double RoadDistanceFactor { get; set; } = 1.3;

    public string Currency { get; set; } = "SLE";
}

public record DeliveryEstimateRequest(string? FromDistrict, string? ToDistrict, double? WeightKg);

public record DeliveryEstimateDto(string FromDistrict, string ToDistrict, double ApproximateDistanceKm, double WeightKg, decimal EstimatedCost, string Currency, string Note);

public class DeliveryEstimateService(IOptions<DeliveryOptions> options)
{
    private const double MaxWeightKg = 50_000;

    public DeliveryEstimateDto Estimate(DeliveryEstimateRequest request)
    {
        var settings = options.Value;
        if (settings.BaseFee is not { } baseFee || settings.RatePerKm is not { } ratePerKm || settings.RatePerKg is not { } ratePerKg)
        {
            throw new ConflictException("Delivery rates have not been set yet, so a cost cannot be estimated.");
        }

        var from = RequireDistrict(request.FromDistrict, "fromDistrict");
        var to = RequireDistrict(request.ToDistrict, "toDistrict");

        if (request.WeightKg is not (> 0 and <= MaxWeightKg))
        {
            throw new ValidationException(
                $"weightKg must be above 0 and at most {MaxWeightKg}.",
                new Dictionary<string, string[]> { ["weightKg"] = [$"weightKg must be above 0 and at most {MaxWeightKg}."] });
        }

        var weightKg = request.WeightKg.Value;
        var distanceKm = RoadDistanceKm(from, to);
        var cost = baseFee + ratePerKm * (decimal)distanceKm + ratePerKg * (decimal)weightKg;

        return new DeliveryEstimateDto(
            from.Name,
            to.Name,
            Math.Round(distanceKm, 1),
            weightKg,
            Math.Round(cost, 2),
            settings.Currency,
            "This is a rough estimate from district to district, not a final price.");
    }

    /// <summary>Approximate road distance between two districts' main towns.</summary>
    public double RoadDistanceKm(District from, District to) =>
        SierraLeoneDistricts.StraightLineKm(from, to) * options.Value.RoadDistanceFactor;

    private static District RequireDistrict(string? value, string field)
    {
        var message = $"{field} must be a Sierra Leone district.";
        return SierraLeoneDistricts.Find(value)
               ?? throw new ValidationException(message, new Dictionary<string, string[]> { [field] = [message] });
    }
}
