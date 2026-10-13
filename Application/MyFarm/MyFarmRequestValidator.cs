using AgrismartAPI.Application.Farms;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Farms;

namespace AgrismartAPI.Application.MyFarm;

/// <summary>Field-level validation for the My Farm requests. Errors are keyed by camelCase field path.</summary>
public static class MyFarmRequestValidator
{
    public const int MaxBoundaryPoints = 200;
    public const decimal MaxCost = 1_000_000_000m;

    public record ValidCrop(
        string CropId, string? PlotName, string? Variety, DateOnly? PlantingDate, DateOnly? ExpectedHarvestDate,
        double? PlotSizeHectares, IReadOnlyList<GeoPoint> Boundary);

    public static ValidCrop? ValidateCrop(FarmCropUpsertRequest? request, CropCatalog catalog, DateOnly today, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (request is null)
        {
            errors["body"] = ["A request body is required."];
            return null;
        }

        var cropId = request.CropId?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(cropId) || !catalog.Contains(cropId))
        {
            errors["cropId"] = ["cropId must be a crop from GET /farms/options."];
        }

        MaxLength(request.PlotName, FarmCrop.MaxPlotNameLength, "plotName", errors);
        MaxLength(request.Variety, FarmCrop.MaxVarietyLength, "variety", errors);
        MaxLength(request.Notes, FarmCrop.MaxNotesLength, "notes", errors);

        if (request.PlantingDate is { } planted && (planted < today.AddYears(-5) || planted > today.AddYears(1)))
        {
            errors["plantingDate"] = ["plantingDate must be within the last 5 years and at most 1 year ahead."];
        }

        if (request.ExpectedHarvestDate is { } harvest)
        {
            if (request.PlantingDate is { } p && harvest <= p)
            {
                errors["expectedHarvestDate"] = ["expectedHarvestDate must be after plantingDate."];
            }
            else if (harvest > today.AddYears(5))
            {
                errors["expectedHarvestDate"] = ["expectedHarvestDate must be within 5 years."];
            }
        }

        double? plotSize = null;
        if (request.AreaUnit is not (AreaUnit.Hectare or AreaUnit.Acre or AreaUnit.SquareMeter))
        {
            errors["areaUnit"] = ["areaUnit must be hectare, acre or squareMeter."];
        }
        else if (request.PlotSize is { } size)
        {
            var hectares = AreaConverter.ToHectares(size, request.AreaUnit, null, null);
            if (size <= 0 || hectares > FarmRequestValidator.MaxAreaHectares)
            {
                errors["plotSize"] = [$"plotSize must be greater than 0 and at most {FarmRequestValidator.MaxAreaHectares} hectares."];
            }
            else
            {
                plotSize = Math.Round(hectares, 4);
            }
        }

        ValidateLocation(request.Latitude, request.Longitude, errors);
        var boundary = ValidateBoundary(request.Boundary, errors);

        return errors.Count > 0
            ? null
            : new ValidCrop(cropId!, request.PlotName, request.Variety, request.PlantingDate, request.ExpectedHarvestDate, plotSize, boundary);
    }

    public static bool ValidateActivity(FarmActivityRequest? request, DateOnly today, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (request is null)
        {
            errors["body"] = ["A request body is required."];
            return false;
        }

        if (!Enum.IsDefined(request.Type))
        {
            errors["type"] = ["type must be one of the activity types in GET /farms/options."];
        }

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > FarmActivity.MaxTitleLength)
        {
            errors["title"] = [$"title is required, up to {FarmActivity.MaxTitleLength} characters."];
        }

        MaxLength(request.Notes, FarmActivity.MaxNotesLength, "notes", errors);

        if (request.DueDate < today.AddYears(-5) || request.DueDate > today.AddYears(2))
        {
            errors["dueDate"] = ["dueDate must be within the last 5 years and at most 2 years ahead."];
        }

        ValidateCost(request.Cost, "cost", errors);
        return errors.Count == 0;
    }

    public static bool ValidateCompletion(CompleteActivityRequest request, DateOnly today, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (request.CompletedOn is { } done && (done > today.AddDays(1) || done < today.AddYears(-5)))
        {
            errors["completedOn"] = ["completedOn cannot be in the future or more than 5 years ago."];
        }

        MaxLength(request.Notes, FarmActivity.MaxNotesLength, "notes", errors);
        ValidateCost(request.Cost, "cost", errors);
        return errors.Count == 0;
    }

    public static bool ValidateHarvest(HarvestCropRequest request, DateOnly today, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (request.HarvestedOn is { } on && (on > today.AddDays(1) || on < today.AddYears(-5)))
        {
            errors["harvestedOn"] = ["harvestedOn cannot be in the future or more than 5 years ago."];
        }

        if (request.Quantity is { } quantity)
        {
            if (quantity <= 0 || quantity > 10_000_000)
            {
                errors["quantity"] = ["quantity must be greater than 0."];
            }

            if (string.IsNullOrWhiteSpace(request.Unit) || request.Unit.Trim().Length > 30)
            {
                errors["unit"] = ["unit is required with quantity (e.g. \"kg\" or \"bags\"), up to 30 characters."];
            }
        }

        MaxLength(request.Notes, 2000, "notes", errors);
        return errors.Count == 0;
    }

    public static IReadOnlyList<GeoPoint>? ValidateMap(FarmMapRequest? request, out Dictionary<string, string[]> errors)
    {
        errors = [];
        if (request is null)
        {
            errors["body"] = ["A request body is required."];
            return null;
        }

        ValidateLocation(request.Latitude, request.Longitude, errors);
        var boundary = ValidateBoundary(request.Boundary, errors);
        return errors.Count == 0 ? boundary : null;
    }

    private static void ValidateLocation(double? latitude, double? longitude, Dictionary<string, string[]> errors)
    {
        if (latitude.HasValue != longitude.HasValue)
        {
            errors["latitude"] = ["latitude and longitude must be given together."];
            return;
        }

        if (latitude is < -90 or > 90)
        {
            errors["latitude"] = ["latitude must be between -90 and 90."];
        }

        if (longitude is < -180 or > 180)
        {
            errors["longitude"] = ["longitude must be between -180 and 180."];
        }
    }

    private static IReadOnlyList<GeoPoint> ValidateBoundary(IReadOnlyList<GeoPointDto>? boundary, Dictionary<string, string[]> errors)
    {
        if (boundary is null || boundary.Count == 0)
        {
            return [];
        }

        if (boundary.Count is < 3 or > MaxBoundaryPoints)
        {
            errors["boundary"] = [$"boundary needs between 3 and {MaxBoundaryPoints} points."];
            return [];
        }

        if (boundary.Any(point => point is null || point.Latitude is < -90 or > 90 || point.Longitude is < -180 or > 180))
        {
            errors["boundary"] = ["every boundary point needs a latitude (-90 to 90) and longitude (-180 to 180)."];
            return [];
        }

        return boundary.Select(point => new GeoPoint(point.Latitude, point.Longitude)).ToList();
    }

    private static void ValidateCost(decimal? cost, string field, Dictionary<string, string[]> errors)
    {
        if (cost is < 0 or > MaxCost)
        {
            errors[field] = [$"{field} must be between 0 and {MaxCost:N0}."];
        }
    }

    private static void MaxLength(string? value, int max, string field, Dictionary<string, string[]> errors)
    {
        if (value is not null && value.Trim().Length > max)
        {
            errors[field] = [$"{field} can be at most {max} characters."];
        }
    }
}
