namespace AgrismartAPI.Domain.Orders;

/// <summary>Snapshots the product's name/price at order time so later catalogue edits don't rewrite order history.</summary>
public record OrderItem(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, Guid SellerId, string SellerName)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
