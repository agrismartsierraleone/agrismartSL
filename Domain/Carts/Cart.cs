using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Carts;

public class Cart : Entity
{
    public Guid UserId { get; private init; }
    public IReadOnlyList<CartItem> Items { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private Cart(Guid userId)
    {
        UserId = userId;
        Items = new List<CartItem>();
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    // EF Core
    private Cart()
    {
        Items = new List<CartItem>();
    }

    public static Cart CreateEmpty(Guid userId) => new(userId);

    public void AddItem(Guid productId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        var items = Items.ToList();
        var index = items.FindIndex(item => item.ProductId == productId);
        if (index >= 0)
        {
            items[index] = items[index] with { Quantity = items[index].Quantity + quantity };
        }
        else
        {
            items.Add(new CartItem(productId, quantity));
        }

        Items = items;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void UpdateItemQuantity(Guid productId, int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");
        }

        var items = Items.ToList();
        var index = items.FindIndex(item => item.ProductId == productId);
        if (index < 0)
        {
            throw new InvalidOperationException("Product is not in the cart.");
        }

        items[index] = items[index] with { Quantity = quantity };
        Items = items;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void RemoveItem(Guid productId)
    {
        Items = Items.Where(item => item.ProductId != productId).ToList();
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void Clear()
    {
        Items = new List<CartItem>();
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
