using AgrismartAPI.Domain.Market;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class SellerRepository(AppDbContext dbContext) : ISellerRepository
{
    public async Task<IReadOnlyList<Seller>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Sellers.OrderBy(seller => seller.Name).ToListAsync(cancellationToken);
    }

    public Task<Seller?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.Sellers.SingleOrDefaultAsync(seller => seller.Id == id, cancellationToken);
    }

    public Task<Seller?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Sellers.SingleOrDefaultAsync(seller => seller.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Seller seller, CancellationToken cancellationToken = default)
    {
        dbContext.Sellers.Add(seller);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Seller seller, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Seller seller, CancellationToken cancellationToken = default)
    {
        dbContext.Sellers.Remove(seller);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
