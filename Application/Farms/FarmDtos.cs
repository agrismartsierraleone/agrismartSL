using AgrismartAPI.Application.Fertilizer;

namespace AgrismartAPI.Application.Farms;

/// <summary>A crop grown on the farm.</summary>
/// <param name="CropId">Crop key from GET /crops or GET /fertilizer/crops, e.g. "cassava" or "rice-lowland".</param>
/// <param name="PlantingDate">When it was planted (yyyy-MM-dd). Optional.</param>
/// <param name="PlotSize">Size of the plot for this crop, in <paramref name="AreaUnit"/>. Optional.</param>
/// <param name="AreaUnit">hectare (default), acre or squareMeter.</param>
public record FarmCropRequest(string CropId, DateOnly? PlantingDate = null, double? PlotSize = null, AreaUnit AreaUnit = AreaUnit.Hectare);

/// <summary>A farm to add for the signed-in farmer.</summary>
/// <param name="Name">Farm name, e.g. "River farm" (required, up to 100 characters).</param>
/// <param name="District">Sierra Leone district, e.g. "Bo" or "Kenema". Optional.</param>
/// <param name="Latitude">Optional; give together with longitude. Used for weather and harvest prediction.</param>
/// <param name="Longitude">Optional; give together with latitude.</param>
/// <param name="Area">Total farm size in <paramref name="AreaUnit"/>. Optional.</param>
/// <param name="AreaUnit">hectare (default), acre or squareMeter.</param>
/// <param name="Crops">Crops grown on the farm. Optional.</param>
public record FarmRequest(
    string Name,
    string? District = null,
    double? Latitude = null,
    double? Longitude = null,
    double? Area = null,
    AreaUnit AreaUnit = AreaUnit.Hectare,
    IReadOnlyList<FarmCropRequest>? Crops = null);

public record FarmCropDto(Guid Id, string CropId, DateOnly? PlantingDate, double? PlotSizeHectares);

public record FarmDto(
    Guid Id,
    string Name,
    string? District,
    double? Latitude,
    double? Longitude,
    double? AreaHectares,
    IReadOnlyList<FarmCropDto> Crops,
    DateTimeOffset CreatedAt);
