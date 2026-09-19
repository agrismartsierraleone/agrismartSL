using AgrismartAPI.Domain.Checkout;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class CheckoutSessionRepository(AppDbContext dbContext) : ICheckoutSessionRepository
{
    public async Task<IReadOnlyList<CheckoutSession>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.CheckoutSessions
            .OrderByDescending(session => session.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<CheckoutSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.CheckoutSessions.SingleOrDefaultAsync(session => session.Id == id, cancellationToken);
    }

    public Task<CheckoutSession?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        return dbContext.CheckoutSessions.SingleOrDefaultAsync(session => session.OrderId == orderId, cancellationToken);
    }

    public async Task AddAsync(CheckoutSession session, CancellationToken cancellationToken = default)
    {
        dbContext.CheckoutSessions.Add(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(CheckoutSession session, CancellationToken cancellationToken = default)
    {
        dbContext.CheckoutSessions.Update(session);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
