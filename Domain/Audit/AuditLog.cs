using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Audit;

public class AuditLog : Entity
{
    public Guid? ActorUserId { get; private set; }
    public string ActorEmail { get; private set; }
    public string Action { get; private set; }
    public Guid? TargetUserId { get; private set; }
    public string? Details { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private AuditLog(Guid? actorUserId, string actorEmail, string action, Guid? targetUserId, string? details)
    {
        ActorUserId = actorUserId;
        ActorEmail = actorEmail;
        Action = action;
        TargetUserId = targetUserId;
        Details = details;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private AuditLog()
    {
        ActorEmail = null!;
        Action = null!;
    }

    public static AuditLog Create(Guid? actorUserId, string actorEmail, string action, Guid? targetUserId = null, string? details = null)
    {
        if (string.IsNullOrWhiteSpace(actorEmail))
        {
            throw new ArgumentException("Actor email is required.", nameof(actorEmail));
        }

        if (string.IsNullOrWhiteSpace(action))
        {
            throw new ArgumentException("Action is required.", nameof(action));
        }

        return new AuditLog(actorUserId, actorEmail.Trim().ToLowerInvariant(), action.Trim(), targetUserId, string.IsNullOrWhiteSpace(details) ? null : details.Trim());
    }
}
