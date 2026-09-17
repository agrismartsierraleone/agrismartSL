using AgrismartAPI.Domain.Scans;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class ScanHistoryRepository(AppDbContext dbContext) : IScanHistoryRepository
{
    public async Task<IReadOnlyList<PlantScan>> GetAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.PlantScans
            .Where(scan => scan.UserId == userId)
            .OrderByDescending(scan => scan.ScannedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(PlantScan scan, CancellationToken cancellationToken = default)
    {
        dbContext.PlantScans.Add(scan);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
