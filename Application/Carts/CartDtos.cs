using AgrismartAPI.Domain.Orders;

namespace AgrismartAPI.Application.Carts;

public record AddCartItemRequest(Guid ProductId, int Quantity);

/// <summary><see cref="PaymentMethod.MonimeOnline"/> is not accepted here — use <c>POST /checkout/sessions</c> for that flow.</summary>
public record CartCheckoutRequest(PaymentMethod PaymentMethod);

public record UpdateCartItemRequest(int Quantity);

public record CartItemDto(
    Guid ProductId,
    string ProductName,
    decimal UnitPrice,
    string Unit,
    int Quantity,
    decimal LineTotal,
    Guid SellerId,
    string SellerName,
    string? ImageUrl);

public record CartDto(Guid Id, IReadOnlyList<CartItemDto> Items, decimal TotalAmount);
