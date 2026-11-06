using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Audit;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Users;

public class UserAppService(
    IUserRepository userRepository,
    ISellerRepository sellerRepository,
    IPasswordHasher passwordHasher,
    ICurrentUserService currentUser,
    AuditLogAppService auditLogAppService,
    UserNotificationService userNotificationService)
{
    public async Task<IReadOnlyList<UserSummaryDto>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await userRepository.GetAllAsync(cancellationToken);
        return users.Select(ToDto).ToList();
    }

    /// <summary>Admin-only: creates a seller or buyer account directly, without going through public self-registration.</summary>
    public async Task<UserSummaryDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new ValidationException("Password must be at least 8 characters long.");
        }

        UserProfileValidator.EnsureValid(request.Role, request.PhoneNumber, request.Nin);

        var existing = await userRepository.GetByEmailAsync(request.Email, cancellationToken);
        if (existing is not null)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        var user = User.Register(request.Name, request.Email, passwordHasher.Hash(request.Password), request.Role, User.TemporaryPasswordLifetime, request.PhoneNumber, request.Nin);
        await userRepository.AddAsync(user, cancellationToken);

        if (user.Role == UserRole.Seller)
        {
            await sellerRepository.AddAsync(Seller.Create(user.Name, user.Email, null, null, user.Id), cancellationToken);
        }

        await auditLogAppService.LogAsync(
            currentUser.UserId,
            currentUser.Email,
            "UserCreated",
            user.Id,
            $"Created {user.Role} account for '{user.Email}'.",
            cancellationToken);

        await userNotificationService.SendAccountCreatedEmailAsync(user, request.Password, isTemporaryPassword: true, cancellationToken);

        return ToDto(user);
    }

    /// <summary>Admin-only: updates a user's name, email, password, phone number and/or NIN. Sends a notification email for any change made.</summary>
    public async Task<UserSummaryDto> UpdateCredentialsAsync(Guid userId, UpdateUserCredentialsRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User '{userId}' was not found.");

        UserProfileValidator.EnsureLengths(request.PhoneNumber, request.Nin);

        var changes = new List<string>();
        string? previousEmail = null;

        if (!string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim() != user.Name)
        {
            user.Rename(request.Name);
            changes.Add($"Name changed to '{user.Name}'.");
        }

        if (!string.IsNullOrWhiteSpace(request.Email) && !string.Equals(request.Email.Trim(), user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var normalizedEmail = request.Email.Trim().ToLowerInvariant();
            var existing = await userRepository.GetByEmailAsync(normalizedEmail, cancellationToken);
            if (existing is not null && existing.Id != user.Id)
            {
                throw new ConflictException("An account with this email already exists.");
            }

            previousEmail = user.Email;
            user.ChangeEmail(normalizedEmail);
            changes.Add($"Email changed from '{previousEmail}' to '{user.Email}'.");
        }

        // These lines also go into the audit log and the notification email, so they never include the values.
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.PhoneNumber.Trim() != user.PhoneNumber)
        {
            user.ChangePhoneNumber(request.PhoneNumber);
            changes.Add("Phone number was updated.");
        }

        if (!string.IsNullOrWhiteSpace(request.Nin) && request.Nin.Trim() != user.Nin)
        {
            user.ChangeNin(request.Nin);
            changes.Add("NIN was updated.");
        }

        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            if (request.NewPassword.Length < 8)
            {
                throw new ValidationException("Password must be at least 8 characters long.");
            }

            user.SetTemporaryPassword(passwordHasher.Hash(request.NewPassword), User.TemporaryPasswordLifetime);
            changes.Add("Password was reset. This is a temporary password and must be changed within 24 hours.");
        }

        if (changes.Count == 0)
        {
            return ToDto(user);
        }

        await userRepository.UpdateAsync(user, cancellationToken);

        await auditLogAppService.LogAsync(
            currentUser.UserId,
            currentUser.Email,
            "UserCredentialsUpdated",
            user.Id,
            string.Join(" ", changes),
            cancellationToken);

        // The password goes in this email only: never in `changes`, which is also written to the audit log.
        if (!string.IsNullOrWhiteSpace(request.NewPassword))
        {
            await userNotificationService.SendCredentialsUpdatedWithTemporaryPasswordEmailAsync(user, changes, request.NewPassword, cancellationToken);
        }
        else
        {
            await userNotificationService.SendCredentialsUpdatedEmailAsync(user, changes, cancellationToken);
        }

        // Tell the old address too, otherwise a mistyped new address means the real owner never hears about it.
        if (previousEmail is not null)
        {
            await userNotificationService.SendAccountUpdatedEmailAsync(previousEmail, user.Name, user.Role, changes, cancellationToken);
        }

        return ToDto(user);
    }

    /// <summary>Self-service: the signed-in user updates their own phone number and/or NIN. Sends a notice for any change made.</summary>
    public async Task<UserSummaryDto> UpdateMyAccountAsync(UpdateMyAccountRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(currentUser.UserId, cancellationToken)
                   ?? throw new NotFoundException($"User '{currentUser.UserId}' was not found.");

        UserProfileValidator.EnsureLengths(request.PhoneNumber, request.Nin);

        var changes = new List<string>();

        // These lines also go into the audit log and the notification email, so they never include the values.
        if (!string.IsNullOrWhiteSpace(request.PhoneNumber) && request.PhoneNumber.Trim() != user.PhoneNumber)
        {
            user.ChangePhoneNumber(request.PhoneNumber);
            changes.Add("Phone number was updated.");
        }

        if (!string.IsNullOrWhiteSpace(request.Nin) && request.Nin.Trim() != user.Nin)
        {
            user.ChangeNin(request.Nin);
            changes.Add("NIN was updated.");
        }

        if (changes.Count == 0)
        {
            return ToDto(user);
        }

        await userRepository.UpdateAsync(user, cancellationToken);

        await auditLogAppService.LogAsync(user.Id, user.Email, "UserAccountUpdated", user.Id, string.Join(" ", changes), cancellationToken);

        await userNotificationService.SendCredentialsUpdatedEmailAsync(user, changes, cancellationToken);

        return ToDto(user);
    }

    public async Task<UserSummaryDto> ChangeRoleAsync(Guid userId, ChangeUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User '{userId}' was not found.");

        var previousRole = user.Role;
        user.ChangeRole(request.Role);
        await userRepository.UpdateAsync(user, cancellationToken);

        if (request.Role == UserRole.Seller && await sellerRepository.GetByUserIdAsync(user.Id, cancellationToken) is null)
        {
            await sellerRepository.AddAsync(Seller.Create(user.Name, user.Email, null, null, user.Id), cancellationToken);
        }

        if (previousRole != user.Role)
        {
            await auditLogAppService.LogAsync(
                currentUser.UserId,
                currentUser.Email,
                "UserRoleChanged",
                user.Id,
                $"Role changed from {previousRole} to {user.Role}.",
                cancellationToken);

            await userNotificationService.SendCredentialsUpdatedEmailAsync(
                user,
                [$"Your account role was changed from {previousRole} to {user.Role}."],
                cancellationToken);
        }

        return ToDto(user);
    }

    /// <summary>Admin-only: permanently deletes a user account. Sends a notification email to the deleted user.</summary>
    public async Task DeleteAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException($"User '{userId}' was not found.");

        if (user.Id == currentUser.UserId)
        {
            throw new ValidationException("You cannot delete your own account.");
        }

        if (await sellerRepository.GetByUserIdAsync(user.Id, cancellationToken) is not null)
        {
            throw new ConflictException("This user has a linked seller profile. Delete or reassign it before deleting the account.");
        }

        await userRepository.DeleteAsync(user, cancellationToken);

        await auditLogAppService.LogAsync(
            currentUser.UserId,
            currentUser.Email,
            "UserDeleted",
            user.Id,
            $"Deleted {user.Role} account for '{user.Email}'.",
            cancellationToken);

        await userNotificationService.SendAccountDeletedEmailAsync(user, cancellationToken);
    }

    private static UserSummaryDto ToDto(User user) => new(user.Id, user.Name, user.Email, user.PhoneNumber, user.Nin, user.Role, user.CreatedAt, user.MustChangePassword, user.PasswordExpiresAt);
}
