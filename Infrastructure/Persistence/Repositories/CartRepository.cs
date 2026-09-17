using AgrismartAPI.Domain.Carts;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class CartRepository(AppDbContext dbContext) : ICartRepository
{
    public Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Carts.SingleOrDefaultAsync(cart => cart.UserId == userId, cancellationToken);
    }

    public async Task AddAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        dbContext.Carts.Add(cart);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(Cart cart, CancellationToken cancellationToken = default)
    {
        dbContext.Carts.Update(cart);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
