using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Scans;

namespace AgrismartAPI.Domain.Farms;

/// <summary>Data access for the My Farm screens. Entities returned are tracked, so changes are saved by <see cref="SaveChangesAsync"/>.</summary>
public interface IMyFarmRepository
{
    /// <summary>The farm with its crops, or null.</summary>
    Task<Farm?> GetFarmAsync(Guid farmId, CancellationToken cancellationToken = default);

    /// <summary>All of the farm's activities, any status.</summary>
    Task<IReadOnlyList<FarmActivity>> GetActivitiesAsync(Guid farmId, CancellationToken cancellationToken = default);

    Task<FarmActivity?> GetActivityAsync(Guid farmId, Guid activityId, CancellationToken cancellationToken = default);

    /// <summary>The farm's scans with their diagnosis, newest first.</summary>
    Task<IReadOnlyList<(FarmScan Link, PlantScan Scan)>> GetScansAsync(Guid farmId, CancellationToken cancellationToken = default);

    /// <summary>The farmer's latest scan from anywhere in the app (the Crop Health fallback).</summary>
    Task<PlantScan?> GetLatestScanForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<PlantScan?> GetScanAsync(Guid scanId, CancellationToken cancellationToken = default);

    /// <summary>Yield records linked to the farm.</summary>
    Task<IReadOnlyList<FarmRecord>> GetYieldRecordsAsync(Guid farmId, CancellationToken cancellationToken = default);

    void Add(object entity);

    void Remove(object entity);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
