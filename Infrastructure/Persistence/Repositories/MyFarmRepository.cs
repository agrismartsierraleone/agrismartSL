using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Farms;
using AgrismartAPI.Domain.Scans;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class MyFarmRepository(AppDbContext dbContext) : IMyFarmRepository
{
    public Task<Farm?> GetFarmAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<Farm>()
            .Include(farm => farm.Crops)
            .SingleOrDefaultAsync(farm => farm.Id == farmId, cancellationToken);
    }

    public async Task<IReadOnlyList<FarmActivity>> GetActivitiesAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<FarmActivity>()
            .Where(activity => activity.FarmId == farmId)
            .OrderBy(activity => activity.DueDate)
            .ThenBy(activity => activity.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<FarmActivity?> GetActivityAsync(Guid farmId, Guid activityId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<FarmActivity>()
            .SingleOrDefaultAsync(activity => activity.Id == activityId && activity.FarmId == farmId, cancellationToken);
    }

    public async Task<IReadOnlyList<(FarmScan Link, PlantScan Scan)>> GetScansAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        var rows = await dbContext.Set<FarmScan>()
            .Where(link => link.FarmId == farmId)
            .Join(dbContext.PlantScans, link => link.PlantScanId, scan => scan.Id, (link, scan) => new { link, scan })
            .OrderByDescending(row => row.scan.ScannedAt)
            .ToListAsync(cancellationToken);

        return rows.Select(row => (row.link, row.scan)).ToList();
    }

    public Task<PlantScan?> GetLatestScanForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.PlantScans
            .Where(scan => scan.UserId == userId)
            .OrderByDescending(scan => scan.ScannedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<PlantScan?> GetScanAsync(Guid scanId, CancellationToken cancellationToken = default)
    {
        return dbContext.PlantScans.SingleOrDefaultAsync(scan => scan.Id == scanId, cancellationToken);
    }

    public async Task<IReadOnlyList<FarmRecord>> GetYieldRecordsAsync(Guid farmId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<FarmRecord>()
            .Where(record => record.FarmId == farmId && record.Type == FarmRecordType.Yield)
            .OrderByDescending(record => record.RecordDate)
            .ToListAsync(cancellationToken);
    }

    public void Add(object entity) => dbContext.Add(entity);

    public void Remove(object entity) => dbContext.Remove(entity);

    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => dbContext.SaveChangesAsync(cancellationToken);
}
