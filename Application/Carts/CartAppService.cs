using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Orders;
using AgrismartAPI.Domain.Carts;
using AgrismartAPI.Domain.Market;

namespace AgrismartAPI.Application.Carts;

public class CartAppService(ICartRepository cartRepository, IMarketRepository marketRepository, OrderAppService orderAppService)
{
    public async Task<CartDto> GetCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<CartDto> AddItemAsync(Guid userId, AddCartItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException("Quantity must be greater than zero.");
        }

        var product = await marketRepository.GetByIdAsync(request.ProductId, cancellationToken)
                      ?? throw new NotFoundException($"Product '{request.ProductId}' was not found.");

        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        cart.AddItem(product.Id, request.Quantity);
        await cartRepository.UpdateAsync(cart, cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<CartDto> UpdateItemAsync(Guid userId, Guid productId, UpdateCartItemRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Quantity <= 0)
        {
            throw new ValidationException("Quantity must be greater than zero.");
        }

        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        if (cart.Items.All(item => item.ProductId != productId))
        {
            throw new NotFoundException($"Product '{productId}' is not in the cart.");
        }

        cart.UpdateItemQuantity(productId, request.Quantity);
        await cartRepository.UpdateAsync(cart, cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task<CartDto> RemoveItemAsync(Guid userId, Guid productId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        cart.RemoveItem(productId);
        await cartRepository.UpdateAsync(cart, cancellationToken);

        return await ToDtoAsync(cart, cancellationToken);
    }

    public async Task ClearCartAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        cart.Clear();
        await cartRepository.UpdateAsync(cart, cancellationToken);
    }

    public async Task<OrderDto> CheckoutAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var cart = await GetOrCreateCartAsync(userId, cancellationToken);
        if (cart.Items.Count == 0)
        {
            throw new ValidationException("Your cart is empty.");
        }

        var orderRequest = new CreateOrderRequest(
            cart.Items.Select(item => new OrderItemRequest(item.ProductId, item.Quantity)).ToList());

        var order = await orderAppService.CreateOrderAsync(userId, orderRequest, cancellationToken);

        cart.Clear();
        await cartRepository.UpdateAsync(cart, cancellationToken);

        return order;
    }

    private async Task<Cart> GetOrCreateCartAsync(Guid userId, CancellationToken cancellationToken)
    {
        var cart = await cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is not null)
        {
            return cart;
        }

        cart = Cart.CreateEmpty(userId);
        await cartRepository.AddAsync(cart, cancellationToken);
        return cart;
    }

    private async Task<CartDto> ToDtoAsync(Cart cart, CancellationToken cancellationToken)
    {
        var itemDtos = new List<CartItemDto>();
        foreach (var item in cart.Items)
        {
            var product = await marketRepository.GetByIdAsync(item.ProductId, cancellationToken);
            if (product is null)
            {
                continue;
            }

            itemDtos.Add(new CartItemDto(
                product.Id,
                product.Name,
                product.Price,
                product.Unit,
                item.Quantity,
                product.Price * item.Quantity,
                product.SellerId,
                product.Seller.Name,
                product.ImageUrl));
        }

        return new CartDto(cart.Id, itemDtos, itemDtos.Sum(item => item.LineTotal));
    }
}
