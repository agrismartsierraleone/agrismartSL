using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Users;

public class User : Entity
{
    public string Name { get; private set; }
    public string Email { get; private set; }
    public string PasswordHash { get; private set; }
    public UserRole Role { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private User(string name, string email, string passwordHash, UserRole role)
    {
        Name = name;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private User()
    {
        Name = null!;
        Email = null!;
        PasswordHash = null!;
    }

    public static User Register(string name, string email, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        return new User(name.Trim(), email.Trim().ToLowerInvariant(), passwordHash, role);
    }

    public void ChangeRole(UserRole newRole) => Role = newRole;
}
