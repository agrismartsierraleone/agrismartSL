using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Assistant;

/// <summary>A farmer's farm: where it is and how big it is. Crops grown on it are <see cref="FarmCrop"/>s.</summary>
public class Farm : Entity
{
    public Guid UserId { get; private init; }
    public string Name { get; private set; }

    /// <summary>Sierra Leone district, e.g. "Bo" or "Kenema".</summary>
    public string? District { get; private set; }

    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public double? AreaHectares { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private readonly List<FarmCrop> _crops = [];
    public IReadOnlyList<FarmCrop> Crops => _crops;

    private Farm(Guid userId, string name, string? district, double? latitude, double? longitude, double? areaHectares)
    {
        UserId = userId;
        Name = name;
        District = district;
        Latitude = latitude;
        Longitude = longitude;
        AreaHectares = areaHectares;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private Farm()
    {
        Name = null!;
    }

    public static Farm Create(Guid userId, string name, string? district, double? latitude, double? longitude, double? areaHectares)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180 || latitude.HasValue != longitude.HasValue)
        {
            throw new ArgumentException("Latitude and longitude must both be given and in range.", nameof(latitude));
        }

        if (areaHectares is <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(areaHectares), "Area must be positive.");
        }

        return new Farm(userId, name.Trim(), string.IsNullOrWhiteSpace(district) ? null : district.Trim(), latitude, longitude, areaHectares);
    }

    public FarmCrop AddCrop(string cropId, DateOnly? plantingDate, double? plotSizeHectares)
    {
        var crop = FarmCrop.Create(Id, cropId, plantingDate, plotSizeHectares);
        _crops.Add(crop);
        return crop;
    }
}
