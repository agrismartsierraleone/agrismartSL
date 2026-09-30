using AgrismartAPI.Domain.Assistant;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class FarmRepository(AppDbContext dbContext) : IFarmRepository
{
    public async Task<IReadOnlyList<Farm>> GetFarmsForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<Farm>()
            .AsNoTracking()
            .Include(farm => farm.Crops)
            .Where(farm => farm.UserId == userId)
            .OrderBy(farm => farm.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FarmRecord>> GetRecordsForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default)
    {
        return await dbContext.Set<FarmRecord>()
            .AsNoTracking()
            .Where(record => record.UserId == userId)
            .OrderByDescending(record => record.RecordDate)
            .ThenByDescending(record => record.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }
}
