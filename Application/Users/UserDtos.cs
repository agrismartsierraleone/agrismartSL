using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Users;

public record UserSummaryDto(Guid Id, string Name, string Email, UserRole Role, DateTimeOffset CreatedAt);

public record ChangeUserRoleRequest(UserRole Role);
