namespace AgrismartAPI.Domain.Farms;

/// <summary>A point on the map (WGS84 degrees).</summary>
public record GeoPoint(double Latitude, double Longitude);

/// <summary>What the farmer has said about a crop. Planned / growing / ready are derived from dates, not stored.</summary>
public enum FarmCropStatus
{
    Active,
    Harvested,
    Failed
}

/// <summary>Kinds of farm work. The first six are also the stages of the crop calendar.</summary>
public enum FarmActivityType
{
    LandPreparation,
    Planting,
    Fertilizing,
    Weeding,
    PestControl,
    Harvesting,
    Irrigation,
    Scouting,
    Other
}

public enum FarmActivityStatus
{
    Pending,
    Completed,
    Skipped
}

public enum FarmActivitySource
{
    /// <summary>Added by the farmer.</summary>
    Manual,

    /// <summary>Generated from the crop calendar when the crop was added or its dates changed.</summary>
    Calendar
}
