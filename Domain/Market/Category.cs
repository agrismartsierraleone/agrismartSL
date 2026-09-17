using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Market;

public class Category : Entity
{
    public string Name { get; private set; }
    public string? Description { get; private set; }

    private Category(string name, string? description)
    {
        Name = name;
        Description = description;
    }

    // EF Core
    private Category()
    {
        Name = null!;
    }

    public static Category Create(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        return new Category(name.Trim(), description?.Trim());
    }

    public void Update(string name, string? description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        Name = name.Trim();
        Description = description?.Trim();
    }
}
