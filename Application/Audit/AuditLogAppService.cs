using AgrismartAPI.Domain.Audit;

namespace AgrismartAPI.Application.Audit;

/// <summary>Records and reads the admin-only activity trail (account creation, credential changes, role changes, etc).</summary>
public class AuditLogAppService(IAuditLogRepository auditLogRepository)
{
    public async Task LogAsync(Guid? actorUserId, string actorEmail, string action, Guid? targetUserId = null, string? details = null, CancellationToken cancellationToken = default)
    {
        var auditLog = AuditLog.Create(actorUserId, actorEmail, action, targetUserId, details);
        await auditLogRepository.AddAsync(auditLog, cancellationToken);
    }

    public async Task<IReadOnlyList<AuditLogDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var logs = await auditLogRepository.GetAllAsync(cancellationToken);
        return logs.Select(ToDto).ToList();
    }

    private static AuditLogDto ToDto(AuditLog log) => new(log.Id, log.ActorUserId, log.ActorEmail, log.Action, log.TargetUserId, log.Details, log.CreatedAt);
}
