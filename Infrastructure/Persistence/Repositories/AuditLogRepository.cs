using AgrismartAPI.Domain.Audit;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
{
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default)
    {
        dbContext.AuditLogs.Add(auditLog);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLog>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.AuditLogs
            .OrderByDescending(log => log.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
