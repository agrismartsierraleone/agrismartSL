using AgrismartAPI.Domain.Orders;

namespace AgrismartAPI.Application.Orders;

public record ProductSales(string ProductName, int Quantity, decimal Revenue);

/// <summary>A seller's own share of their orders. Other sellers' items in the same order are never counted.</summary>
/// <param name="Received">Reconciled as paid.</param>
/// <param name="Confirmed">Confirmed as received, waiting for final reconciliation.</param>
/// <param name="Pending">Not paid yet.</param>
public record SellerEarnings(int Days, int Orders, decimal Received, decimal Confirmed, decimal Pending, IReadOnlyList<ProductSales> TopProducts);

public static class SellerEarningsCalculator
{
    private const int TopProductCount = 5;

    /// <summary>Calculated from order records. Cancelled orders earn nothing, so they are left out.</summary>
    public static SellerEarnings Calculate(IEnumerable<Order> orders, Guid sellerId, int days, DateTimeOffset now)
    {
        var since = now.AddDays(-days);
        var sellerOrders = orders
            .Where(order => order.Status != OrderStatus.Cancelled && order.CreatedAt >= since)
            .Where(order => order.Items.Any(item => item.SellerId == sellerId))
            .ToList();

        decimal received = 0, confirmed = 0, pending = 0;
        foreach (var order in sellerOrders)
        {
            var subtotal = order.Items.Where(item => item.SellerId == sellerId).Sum(item => item.LineTotal);
            switch (order.GetSellerPayment(sellerId)?.PaymentStatus)
            {
                case PaymentStatus.Paid:
                    received += subtotal;
                    break;
                case PaymentStatus.Confirmed:
                    confirmed += subtotal;
                    break;
                default:
                    pending += subtotal;
                    break;
            }
        }

        var topProducts = sellerOrders
            .SelectMany(order => order.Items)
            .Where(item => item.SellerId == sellerId)
            .GroupBy(item => item.ProductName)
            .Select(group => new ProductSales(group.Key, group.Sum(item => item.Quantity), group.Sum(item => item.LineTotal)))
            .OrderByDescending(product => product.Revenue)
            .Take(TopProductCount)
            .ToList();

        return new SellerEarnings(days, sellerOrders.Count, received, confirmed, pending, topProducts);
    }
}
