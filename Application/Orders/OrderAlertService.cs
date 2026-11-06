using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Orders;

public record OrderAlertDto(Guid OrderId, OrderCustomerDto Customer, decimal TotalAmount, OrderStatus Status, DateTimeOffset CreatedAt, IReadOnlyList<string> Reasons);

/// <summary>
/// Flags recent orders for an admin to look at, using fixed rules so every alert can be explained. It only
/// reports: it never cancels an order or blocks a buyer, and a flag does not mean an order is fraudulent.
/// </summary>
public class OrderAlertService(IOrderRepository orderRepository, IUserRepository userRepository, ISellerRepository sellerRepository)
{
    private static readonly TimeSpan LookBack = TimeSpan.FromDays(30);
    private static readonly TimeSpan DuplicateWindow = TimeSpan.FromHours(1);
    private const int MinOrdersForTypicalValue = 10;
    private const decimal HighValueMultiple = 5;
    private const int RepeatedCancellations = 3;

    public async Task<IReadOnlyList<OrderAlertDto>> GetAlertsAsync(CancellationToken cancellationToken = default)
    {
        var allOrders = await orderRepository.GetAllAsync(cancellationToken);
        var since = DateTimeOffset.UtcNow - LookBack;
        var recentOrders = allOrders.Where(order => order.CreatedAt >= since).ToList();

        var sellerIdsOwnedByUser = (await sellerRepository.GetAllAsync(cancellationToken))
            .Where(seller => seller.UserId.HasValue)
            .ToLookup(seller => seller.UserId!.Value, seller => seller.Id);

        var typicalValue = Median(allOrders.Where(order => order.Status != OrderStatus.Cancelled).Select(order => order.TotalAmount).ToList());
        var hasEnoughHistory = allOrders.Count(order => order.Status != OrderStatus.Cancelled) >= MinOrdersForTypicalValue;

        var cancellationsByBuyer = recentOrders
            .Where(order => order.Status == OrderStatus.Cancelled)
            .GroupBy(order => order.UserId)
            .ToDictionary(group => group.Key, group => group.Count());

        var flagged = new List<(Order Order, List<string> Reasons)>();
        foreach (var order in recentOrders)
        {
            var reasons = new List<string>();

            if (order.Status != OrderStatus.Cancelled)
            {
                if (hasEnoughHistory && order.TotalAmount >= typicalValue * HighValueMultiple)
                {
                    reasons.Add($"Order value is at least {HighValueMultiple} times a typical order ({typicalValue:0.##}).");
                }

                if (recentOrders.Any(other => IsDuplicateOf(order, other)))
                {
                    reasons.Add("The same buyer placed the same items again within an hour.");
                }

                var ownSellerIds = sellerIdsOwnedByUser[order.UserId].ToHashSet();
                if (order.Items.Any(item => ownSellerIds.Contains(item.SellerId)))
                {
                    reasons.Add("The buyer is also the seller of an item in this order.");
                }
            }

            if (cancellationsByBuyer.GetValueOrDefault(order.UserId) >= RepeatedCancellations)
            {
                reasons.Add($"This buyer has {cancellationsByBuyer[order.UserId]} cancelled orders in the last {LookBack.Days} days.");
            }

            if (reasons.Count > 0)
            {
                flagged.Add((order, reasons));
            }
        }

        var users = (await userRepository.GetByIdsAsync(flagged.Select(entry => entry.Order.UserId).Distinct(), cancellationToken))
            .ToDictionary(user => user.Id);

        return flagged
            .OrderByDescending(entry => entry.Order.CreatedAt)
            .Select(entry =>
            {
                var user = users.GetValueOrDefault(entry.Order.UserId);
                return new OrderAlertDto(
                    entry.Order.Id,
                    new OrderCustomerDto(entry.Order.UserId, user?.Name ?? "Unknown", user?.Email ?? "unknown"),
                    entry.Order.TotalAmount,
                    entry.Order.Status,
                    entry.Order.CreatedAt,
                    entry.Reasons);
            })
            .ToList();
    }

    /// <summary>True when <paramref name="earlier"/> is an earlier order by the same buyer with the same items, placed shortly before.</summary>
    private static bool IsDuplicateOf(Order order, Order earlier) =>
        earlier.Id != order.Id
        && earlier.UserId == order.UserId
        && earlier.Status != OrderStatus.Cancelled
        && earlier.CreatedAt <= order.CreatedAt
        && order.CreatedAt - earlier.CreatedAt <= DuplicateWindow
        && Fingerprint(earlier) == Fingerprint(order);

    private static string Fingerprint(Order order) =>
        string.Join(",", order.Items.OrderBy(item => item.ProductId).Select(item => $"{item.ProductId}:{item.Quantity}"));

    private static decimal Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        var sorted = values.Order().ToList();
        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
    }
}
