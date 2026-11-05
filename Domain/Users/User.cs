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

    /// <summary>Nullable in the database for accounts created before this field existed. The API requires it for new buyers and sellers.</summary>
    public string? PhoneNumber { get; private set; }

    /// <summary>National Identification Number. Nullable in the database for the same reason as <see cref="PhoneNumber"/>.</summary>
    public string? Nin { get; private set; }

    public DateTimeOffset CreatedAt { get; private init; }

    /// <summary>True while the current password is an admin-issued temporary one the user hasn't replaced yet.</summary>
    public bool MustChangePassword { get; private set; }

    /// <summary>When the current temporary password stops being accepted for login. Null once the user sets their own password.</summary>
    public DateTimeOffset? PasswordExpiresAt { get; private set; }

    private User(string name, string email, string passwordHash, UserRole role, bool mustChangePassword, DateTimeOffset? passwordExpiresAt, string? phoneNumber, string? nin)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
        MustChangePassword = mustChangePassword;
        PasswordExpiresAt = passwordExpiresAt;
        PhoneNumber = phoneNumber;
        Nin = nin;
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
    public static User Register(string name, string email, string passwordHash, UserRole role, TimeSpan? temporaryPasswordLifetime = null, string? phoneNumber = null, string? nin = null)
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

        return new User(name.Trim(), email.Trim().ToLowerInvariant(), passwordHash, role, temporaryPasswordLifetime.HasValue, expiresAt, Normalize(phoneNumber), Normalize(nin));
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

    public void ChangePhoneNumber(string phoneNumber) => PhoneNumber = Normalize(phoneNumber);

    public void ChangeNin(string nin) => Nin = Normalize(nin);

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

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
