using AgrismartAPI.Domain.Harvest;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class CropRepository(AppDbContext dbContext) : ICropRepository
{
    public async Task<IReadOnlyList<CropProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Crops.OrderBy(crop => crop.Name).ToListAsync(cancellationToken);
    }

    public Task<CropProfile?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return dbContext.Crops.SingleOrDefaultAsync(crop => crop.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(CropProfile crop, CancellationToken cancellationToken = default)
    {
        dbContext.Crops.Add(crop);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
