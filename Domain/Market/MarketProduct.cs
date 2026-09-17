using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Market;

public class MarketProduct : Entity
{
    public string Name { get; private set; }
    public Guid CategoryId { get; private set; }
    public Category Category { get; private set; } = null!;
    public decimal Price { get; private set; }
    public string Unit { get; private set; }
    public Guid SellerId { get; private set; }
    public Seller Seller { get; private set; } = null!;
    public string Description { get; private set; }
    public string? ImageUrl { get; private set; }

    private MarketProduct(
        string name,
        Guid categoryId,
        decimal price,
        string unit,
        Guid sellerId,
        string description,
        string? imageUrl)
    {
        Name = name;
        CategoryId = categoryId;
        Price = price;
        Unit = unit;
        SellerId = sellerId;
        Description = description;
        ImageUrl = imageUrl;
    }

    // EF Core
    private MarketProduct()
    {
        Name = null!;
        Unit = null!;
        Description = null!;
    }

    public static MarketProduct Create(
        string name,
        Guid categoryId,
        decimal price,
        string unit,
        Guid sellerId,
        string description,
        string? imageUrl = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        return new MarketProduct(name.Trim(), categoryId, price, unit, sellerId, description, imageUrl);
    }

    public void Update(
        string name,
        Guid categoryId,
        decimal price,
        string unit,
        Guid sellerId,
        string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        Name = name.Trim();
        CategoryId = categoryId;
        Price = price;
        Unit = unit;
        SellerId = sellerId;
        Description = description;
    }

    public void SetImageUrl(string? imageUrl) => ImageUrl = imageUrl;
}
