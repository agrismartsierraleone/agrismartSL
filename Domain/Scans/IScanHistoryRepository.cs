namespace AgrismartAPI.Domain.Scans;

public interface IScanHistoryRepository
{
    Task<IReadOnlyList<PlantScan>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(PlantScan scan, CancellationToken cancellationToken = default);
}
