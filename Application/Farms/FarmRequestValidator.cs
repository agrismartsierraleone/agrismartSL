using AgrismartAPI.Application.Fertilizer;

namespace AgrismartAPI.Application.Farms;

/// <summary>Field-level validation for <see cref="FarmRequest"/>. Pure: the caller supplies the known crop ids.</summary>
public static class FarmRequestValidator
{
    public const int MaxNameLength = 100;
    public const double MaxAreaHectares = 10_000;

    /// <summary>The 16 districts of Sierra Leone (2017 boundaries).</summary>
    public static readonly IReadOnlyList<string> Districts =
    [
        "Bo", "Bombali", "Bonthe", "Falaba", "Kailahun", "Kambia", "Karene", "Kenema",
        "Koinadugu", "Kono", "Moyamba", "Port Loko", "Pujehun", "Tonkolili",
        "Western Area Rural", "Western Area Urban"
    ];

    /// <summary>Validated, normalized values ready for <c>Farm.Create</c> / <c>Farm.AddCrop</c>.</summary>
    public record ValidFarm(string Name, string? District, double? AreaHectares, IReadOnlyList<(string CropId, DateOnly? PlantingDate, double? PlotSizeHectares)> Crops);

    /// <summary>Returns the normalized farm, or null with <paramref name="errors"/> filled (keys are camelCase field paths).</summary>
    public static ValidFarm? Validate(FarmRequest? request, IReadOnlySet<string> knownCropIds, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (request is null)
        {
            errors["body"] = ["A request body is required."];
            return null;
        }

        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > MaxNameLength)
        {
            errors["name"] = [$"name is required, up to {MaxNameLength} characters."];
        }

        string? district = null;
        if (!string.IsNullOrWhiteSpace(request.District))
        {
            district = Districts.FirstOrDefault(d => string.Equals(d, request.District.Trim(), StringComparison.OrdinalIgnoreCase));
            if (district is null)
            {
                errors["district"] = [$"district must be one of: {string.Join(", ", Districts)}."];
            }
        }

        if (request.Latitude.HasValue != request.Longitude.HasValue)
        {
            errors["latitude"] = ["latitude and longitude must be given together."];
        }
        else if (request.Latitude is < -90 or > 90)
        {
            errors["latitude"] = ["latitude must be between -90 and 90."];
        }

        if (request.Longitude is < -180 or > 180)
        {
            errors["longitude"] = ["longitude must be between -180 and 180."];
        }

        var areaHectares = ToHectares(request.Area, request.AreaUnit, "area", "areaUnit", errors);

        var crops = new List<(string, DateOnly?, double?)>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var requested = request.Crops ?? [];
        for (var i = 0; i < requested.Count; i++)
        {
            var prefix = $"crops[{i}].";
            var crop = requested[i];
            var cropId = crop?.CropId?.Trim().ToLowerInvariant();
            if (crop is null || string.IsNullOrEmpty(cropId) || !knownCropIds.Contains(cropId))
            {
                errors[$"{prefix}cropId"] = ["cropId must be a crop from GET /crops or GET /fertilizer/crops."];
                continue;
            }

            if (crop.PlantingDate is { } planted && (planted < today.AddYears(-5) || planted > today.AddYears(1)))
            {
                errors[$"{prefix}plantingDate"] = ["plantingDate must be within the last 5 years and at most 1 year ahead."];
            }

            crops.Add((cropId, crop.PlantingDate, ToHectares(crop.PlotSize, crop.AreaUnit, $"{prefix}plotSize", $"{prefix}areaUnit", errors)));
        }

        return errors.Count > 0 ? null : new ValidFarm(name!, district, areaHectares, crops);
    }

    /// <summary>Same units as the fertilizer calculator, minus plotDimensions.</summary>
    private static double? ToHectares(double? area, AreaUnit unit, string areaField, string unitField, Dictionary<string, string[]> errors)
    {
        if (unit is not (AreaUnit.Hectare or AreaUnit.Acre or AreaUnit.SquareMeter))
        {
            errors[unitField] = [$"{unitField} must be hectare, acre or squareMeter."];
            return null;
        }

        if (area is null)
        {
            return null;
        }

        var hectares = AreaConverter.ToHectares(area, unit, null, null);
        if (area <= 0 || hectares > MaxAreaHectares)
        {
            errors[areaField] = [$"{areaField} must be greater than 0 and at most {MaxAreaHectares} hectares."];
            return null;
        }

        return Math.Round(hectares, 4);
    }
}
