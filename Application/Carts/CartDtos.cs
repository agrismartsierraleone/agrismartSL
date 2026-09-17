namespace AgrismartAPI.Application.Carts;

public record AddCartItemRequest(Guid ProductId, int Quantity);

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
