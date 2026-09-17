namespace AgrismartAPI.Domain.Audit;

public interface IAuditLogRepository
{
    Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AuditLog>> GetAllAsync(CancellationToken cancellationToken = default);
}
