using AgrismartAPI.Domain.Fertilizer;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class FertilizerCropRepository(AppDbContext dbContext) : IFertilizerCropRepository
{
    public async Task<IReadOnlyList<FertilizerCropProfile>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.FertilizerCrops.OrderBy(crop => crop.Name).ToListAsync(cancellationToken);
    }

    public Task<FertilizerCropProfile?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return dbContext.FertilizerCrops.SingleOrDefaultAsync(crop => crop.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(FertilizerCropProfile crop, CancellationToken cancellationToken = default)
    {
        dbContext.FertilizerCrops.Add(crop);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FertilizerCropProfile crop, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
