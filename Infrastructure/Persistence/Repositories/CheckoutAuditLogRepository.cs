using AgrismartAPI.Domain.Checkout;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class CheckoutAuditLogRepository(AppDbContext dbContext) : ICheckoutAuditLogRepository
{
    public async Task AddAsync(CheckoutAuditLog auditLog, CancellationToken cancellationToken = default)
    {
        dbContext.CheckoutAuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CheckoutAuditLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.CheckoutAuditLogs
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CheckoutAuditLog>> GetByCheckoutSessionIdAsync(Guid checkoutSessionId, CancellationToken cancellationToken = default)
    {
        return await dbContext.CheckoutAuditLogs
            .Where(log => log.CheckoutSessionId == checkoutSessionId)
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
