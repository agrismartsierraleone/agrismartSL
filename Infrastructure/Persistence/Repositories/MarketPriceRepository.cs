using AgrismartAPI.Domain.Market;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class MarketPriceRepository(AppDbContext dbContext) : IMarketPriceRepository
{
    public async Task<IReadOnlyList<MarketPrice>> GetBetweenAsync(DateOnly from, DateOnly to, MarketPriceCategory? category = null, string? crop = null, string? district = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.MarketPrices.Where(price => price.PriceDate >= from && price.PriceDate <= to);
        if (category is not null)
        {
            query = query.Where(price => price.Category == category);
        }

        if (crop is not null)
        {
            query = query.Where(price => price.Crop == crop);
        }

        if (district is not null)
        {
            query = query.Where(price => price.District == district);
        }

        return await query.OrderByDescending(price => price.PriceDate).ToListAsync(cancellationToken);
    }

    public Task<MarketPrice?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.MarketPrices.SingleOrDefaultAsync(price => price.Id == id, cancellationToken);

    public async Task SaveAsync(IReadOnlyList<MarketPrice> newPrices, CancellationToken cancellationToken = default)
    {
        dbContext.MarketPrices.AddRange(newPrices);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(MarketPrice price, CancellationToken cancellationToken = default)
    {
        dbContext.MarketPrices.Remove(price);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
