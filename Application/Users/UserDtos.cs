using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Users;

public record UserSummaryDto(Guid Id, string Name, string Email, UserRole Role, DateTimeOffset CreatedAt);

public record ChangeUserRoleRequest(UserRole Role);

/// <summary>Admin-only: creates a seller or buyer account directly (bypasses public self-registration).</summary>
public record CreateUserRequest(string Name, string Email, string Password, UserRole Role);
