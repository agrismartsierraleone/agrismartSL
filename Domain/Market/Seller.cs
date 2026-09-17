using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Market;

public class Seller : Entity
{
    public string Name { get; private set; }
    public string? ContactEmail { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Location { get; private set; }
    public Guid? UserId { get; private set; }

    private Seller(string name, string? contactEmail, string? contactPhone, string? location, Guid? userId)
    {
        Name = name;
        ContactEmail = contactEmail;
        ContactPhone = contactPhone;
        Location = location;
        UserId = userId;
    }

    // EF Core
    private Seller()
    {
        Name = null!;
    }

    public static Seller Create(string name, string? contactEmail, string? contactPhone, string? location, Guid? userId = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new Seller(name.Trim(), contactEmail?.Trim(), contactPhone?.Trim(), location?.Trim(), userId);
    }

    public void Update(string name, string? contactEmail, string? contactPhone, string? location)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name.Trim();
        ContactEmail = contactEmail?.Trim();
        ContactPhone = contactPhone?.Trim();
        Location = location?.Trim();
    }
}
