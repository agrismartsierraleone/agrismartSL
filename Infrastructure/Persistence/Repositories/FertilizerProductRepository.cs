using AgrismartAPI.Domain.Fertilizer;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class FertilizerProductRepository(AppDbContext dbContext) : IFertilizerProductRepository
{
    public async Task<IReadOnlyList<FertilizerProduct>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.FertilizerProducts.OrderBy(product => product.Name).ToListAsync(cancellationToken);
    }

    public Task<FertilizerProduct?> GetByKeyAsync(string key, CancellationToken cancellationToken = default)
    {
        var normalized = key.Trim().ToLowerInvariant();
        return dbContext.FertilizerProducts.SingleOrDefaultAsync(product => product.Key == normalized, cancellationToken);
    }

    public async Task AddAsync(FertilizerProduct product, CancellationToken cancellationToken = default)
    {
        dbContext.FertilizerProducts.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(FertilizerProduct product, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
