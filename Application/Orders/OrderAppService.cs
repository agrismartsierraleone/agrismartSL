using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Orders;

public class OrderAppService(
    IOrderRepository orderRepository,
    IMarketRepository marketRepository,
    IUserRepository userRepository,
    ISellerRepository sellerRepository)
{
    public async Task<OrderDto> CreateOrderAsync(Guid userId, CreateOrderRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Items.Count == 0)
        {
            throw new ValidationException("An order must contain at least one item.");
        }

        var items = new List<OrderItem>();
        foreach (var line in request.Items)
        {
            if (line.Quantity <= 0)
            {
                throw new ValidationException("Item quantity must be greater than zero.");
            }

            var product = await marketRepository.GetByIdAsync(line.ProductId, cancellationToken)
                          ?? throw new NotFoundException($"Product '{line.ProductId}' was not found.");

            items.Add(new OrderItem(product.Id, product.Name, product.Price, line.Quantity, product.SellerId, product.Seller.Name));
        }

        var order = Order.Create(userId, items);
        await orderRepository.AddAsync(order, cancellationToken);
        return ToDto(order);
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var orders = await orderRepository.GetAllForUserAsync(userId, cancellationToken);
        return orders.Select(ToDto).ToList();
    }

    public async Task<OrderWithCustomerDto> GetOrderAsync(Guid userId, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken);
        if (order is null || order.UserId != userId)
        {
            // Don't distinguish "doesn't exist" from "belongs to someone else".
            throw new NotFoundException($"Order '{orderId}' was not found.");
        }

        var user = await userRepository.GetByIdAsync(order.UserId, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        return ToDtoWithCustomer(order, user);
    }

    public async Task<IReadOnlyList<OrderWithCustomerDto>> GetAllOrdersAsync(CancellationToken cancellationToken = default)
    {
        var orders = await orderRepository.GetAllAsync(cancellationToken);

        var userIds = orders.Select(order => order.UserId).Distinct().ToList();
        var users = await userRepository.GetByIdsAsync(userIds, cancellationToken);
        var usersById = users.ToDictionary(user => user.Id);

        return orders
            .Select(order => ToDtoWithCustomer(order, usersById.GetValueOrDefault(order.UserId)))
            .ToList();
    }

    public async Task<IReadOnlyList<OrderForSellerDto>> GetOrdersForSellerAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var seller = await sellerRepository.GetByUserIdAsync(userId, cancellationToken)
                     ?? throw new NotFoundException("No seller profile is linked to this account.");

        var orders = (await orderRepository.GetAllAsync(cancellationToken))
            .Where(order => order.Items.Any(item => item.SellerId == seller.Id))
            .ToList();

        var userIds = orders.Select(order => order.UserId).Distinct().ToList();
        var users = await userRepository.GetByIdsAsync(userIds, cancellationToken);
        var usersById = users.ToDictionary(user => user.Id);

        return orders
            .Select(order => ToDtoForSeller(order, seller.Id, usersById.GetValueOrDefault(order.UserId)))
            .ToList();
    }

    private static OrderItemDto ToDto(OrderItem item) => new(
        item.ProductId,
        item.ProductName,
        item.UnitPrice,
        item.Quantity,
        item.LineTotal,
        item.SellerId,
        item.SellerName);

    private static OrderDto ToDto(Order order) => new(
        order.Id,
        order.UserId,
        order.Items.Select(ToDto).ToList(),
        order.TotalAmount,
        order.Status,
        order.CreatedAt);

    private static OrderWithCustomerDto ToDtoWithCustomer(Order order, User? user) => new(
        order.Id,
        new OrderCustomerDto(order.UserId, user?.Name ?? "Unknown", user?.Email ?? "unknown"),
        order.Items.Select(ToDto).ToList(),
        order.TotalAmount,
        order.Status,
        order.CreatedAt);

    private static OrderForSellerDto ToDtoForSeller(Order order, Guid sellerId, User? user)
    {
        var sellerItems = order.Items.Where(item => item.SellerId == sellerId).Select(ToDto).ToList();
        return new OrderForSellerDto(
            order.Id,
            new OrderCustomerDto(order.UserId, user?.Name ?? "Unknown", user?.Email ?? "unknown"),
            sellerItems,
            sellerItems.Sum(item => item.LineTotal),
            order.Status,
            order.CreatedAt);
    }
}
