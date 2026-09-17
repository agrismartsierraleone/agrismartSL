using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Users;

public class User : Entity
{
    /// <summary>How long an admin-issued temporary password remains valid for login before it must be reset.</summary>
    public static readonly TimeSpan TemporaryPasswordLifetime = TimeSpan.FromHours(24);

    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>True while the current password is an admin-issued temporary one the user hasn't replaced yet.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>When the current temporary password stops being accepted for login. Null once the user sets their own password.</summary>
    public DateTimeOffset? PasswordExpiresAt { get; private set; }

    private User(string name, string email, string passwordHash, UserRole role, bool mustChangePassword, DateTimeOffset? passwordExpiresAt)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
        MustChangePassword = mustChangePassword;
        PasswordExpiresAt = passwordExpiresAt;
    }

    // EF Core
    private User()
    {
        Name = null!;
        Email = null!;
        PasswordHash = null!;
    }

    /// <param name="temporaryPasswordLifetime">
    /// When set, <paramref name="passwordHash"/> is treated as an admin-issued temporary password: the
    /// account is flagged to force a change, and the password stops working for login after this long.
    /// </param>
    public static User Register(string name, string email, string passwordHash, UserRole role, TimeSpan? temporaryPasswordLifetime = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        var expiresAt = temporaryPasswordLifetime.HasValue ? DateTimeOffset.UtcNow.Add(temporaryPasswordLifetime.Value) : (DateTimeOffset?)null;

        return new User(name.Trim(), email.Trim().ToLowerInvariant(), passwordHash, role, temporaryPasswordLifetime.HasValue, expiresAt);
    }

    public void ChangeRole(UserRole newRole) => Role = newRole;

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name.Trim();
    }

    public void ChangeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        Email = email.Trim().ToLowerInvariant();
    }

    /// <summary>Sets an admin-issued temporary password: the account must change it before <paramref name="lifetime"/> elapses.</summary>
    public void SetTemporaryPassword(string passwordHash, TimeSpan lifetime)
    {
        PasswordHash = passwordHash;
        MustChangePassword = true;
        PasswordExpiresAt = DateTimeOffset.UtcNow.Add(lifetime);
    }

    /// <summary>The user sets their own password (first-login reset or a voluntary change) — no longer temporary.</summary>
    public void CompletePasswordReset(string passwordHash)
    {
        PasswordHash = passwordHash;
        MustChangePassword = false;
        PasswordExpiresAt = null;
    }

    public bool HasExpiredTemporaryPassword() => MustChangePassword && PasswordExpiresAt is { } expiresAt && DateTimeOffset.UtcNow > expiresAt;
}
