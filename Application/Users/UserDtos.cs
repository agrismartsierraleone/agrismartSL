using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Users;

public record UserSummaryDto(Guid Id, string Name, string Email, string? PhoneNumber, string? Nin, UserRole Role, DateTimeOffset CreatedAt, bool MustChangePassword, DateTimeOffset? PasswordExpiresAt);

public record ChangeUserRoleRequest(UserRole Role);

/// <summary>Admin-only: creates a seller or buyer account directly (bypasses public self-registration). PhoneNumber and Nin are required for buyers and sellers, and optional for admins.</summary>
public record CreateUserRequest(string Name, string Email, string Password, UserRole Role, string? PhoneNumber = null, string? Nin = null);

/// <summary>Admin-only: updates a user's name, email, password, phone number and/or NIN. Any changed field triggers a notification email.</summary>
public record UpdateUserCredentialsRequest(string? Name, string? Email, string? NewPassword, string? PhoneNumber = null, string? Nin = null);
