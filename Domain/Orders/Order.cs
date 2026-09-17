using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Orders;

public class Order : Entity
{
    public Guid UserId { get; private init; }
    public IReadOnlyList<OrderItem> Items { get; private init; }
    public decimal TotalAmount { get; private init; }
    public OrderStatus Status { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private Order(Guid userId, IReadOnlyList<OrderItem> items)
    {
        UserId = userId;
        Items = items;
        TotalAmount = items.Sum(item => item.LineTotal);
        Status = OrderStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private Order()
    {
        Items = new List<OrderItem>();
    }

    public static Order Create(Guid userId, IReadOnlyList<OrderItem> items)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        if (items.Any(item => item.Quantity <= 0))
        {
            throw new ArgumentException("Item quantity must be greater than zero.", nameof(items));
        }

        return new Order(userId, items);
    }
}
