using AgrismartAPI.Domain.Market;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class MarketRepository(AppDbContext dbContext) : IMarketRepository
{
    public async Task<IReadOnlyList<MarketProduct>> GetAllAsync(Guid? categoryId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.MarketProducts
            .Include(product => product.Category)
            .Include(product => product.Seller)
            .AsQueryable();

        if (categoryId is not null)
        {
            query = query.Where(product => product.CategoryId == categoryId);
        }

        return await query.OrderBy(product => product.Name).ToListAsync(cancellationToken);
    }

    public Task<MarketProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.MarketProducts
            .Include(product => product.Category)
            .Include(product => product.Seller)
            .SingleOrDefaultAsync(product => product.Id == id, cancellationToken);
    }

    public Task<bool> ExistsForCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default)
    {
        return dbContext.MarketProducts.AnyAsync(product => product.CategoryId == categoryId, cancellationToken);
    }

    public Task<bool> ExistsForSellerAsync(Guid sellerId, CancellationToken cancellationToken = default)
    {
        return dbContext.MarketProducts.AnyAsync(product => product.SellerId == sellerId, cancellationToken);
    }

    public async Task AddAsync(MarketProduct product, CancellationToken cancellationToken = default)
    {
        dbContext.MarketProducts.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(MarketProduct product, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(MarketProduct product, CancellationToken cancellationToken = default)
    {
        dbContext.MarketProducts.Remove(product);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
