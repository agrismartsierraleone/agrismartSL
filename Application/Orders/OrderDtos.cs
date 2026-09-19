using AgrismartAPI.Domain.Orders;

namespace AgrismartAPI.Application.Orders;

public record OrderItemRequest(Guid ProductId, int Quantity);

/// <summary><see cref="PaymentMethod.MonimeOnline"/> is not accepted here — use <c>POST /checkout/sessions</c> for that flow.</summary>
public record CreateOrderRequest(IReadOnlyList<OrderItemRequest> Items, PaymentMethod PaymentMethod);

public record OrderItemDto(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal, Guid SellerId, string SellerName);

public record OrderDto(
    Guid Id,
    Guid UserId,
    IReadOnlyList<OrderItemDto> Items,
    decimal TotalAmount,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateTimeOffset? HoldExpiresAt,
    Guid? CheckoutSessionId);

public record OrderCustomerDto(Guid Id, string Name, string Email);

public record OrderWithCustomerDto(
    Guid Id,
    OrderCustomerDto Customer,
    IReadOnlyList<OrderItemDto> Items,
    decimal TotalAmount,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    DateTimeOffset? HoldExpiresAt,
    Guid? CheckoutSessionId);

public record OrderForSellerDto(
    Guid Id,
    OrderCustomerDto Customer,
    IReadOnlyList<OrderItemDto> Items,
    decimal Subtotal,
    OrderStatus Status,
    DateTimeOffset CreatedAt,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus);
