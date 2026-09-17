namespace AgrismartAPI.Application.Audit;

public record AuditLogDto(Guid Id, Guid? ActorUserId, string ActorEmail, string Action, Guid? TargetUserId, string? Details, DateTimeOffset CreatedAt);
