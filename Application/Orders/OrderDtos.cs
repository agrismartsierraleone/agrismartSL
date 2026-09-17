using AgrismartAPI.Domain.Orders;

namespace AgrismartAPI.Application.Orders;

public record OrderItemRequest(Guid ProductId, int Quantity);

public record CreateOrderRequest(IReadOnlyList<OrderItemRequest> Items);

public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal, Guid SellerId, string SellerName);

public record OrderDto(
    Guid Id,
    Guid UserId,
    IReadOnlyList<OrderItemDto> Items,
    decimal TotalAmount,
    OrderStatus Status,
    DateTimeOffset CreatedAt);

public record OrderCustomerDto(Guid Id, string Name, string Email);

public record OrderWithCustomerDto(
    Guid Id,
    OrderCustomerDto Customer,
    IReadOnlyList<OrderItemDto> Items,
    decimal TotalAmount,
    OrderStatus Status,
    DateTimeOffset CreatedAt);

public record OrderForSellerDto(
    Guid Id,
    OrderCustomerDto Customer,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    OrderStatus Status,
    DateTimeOffset CreatedAt);
