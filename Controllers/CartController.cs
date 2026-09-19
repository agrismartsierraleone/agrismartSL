using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Carts;
using AgrismartAPI.Application.Orders;
using AgrismartAPI.Controllers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("cart")]
[Authorize(Roles = "Buyer")]
public class CartController(CartAppService cartAppService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CartDto>> GetCart(CancellationToken cancellationToken)
    {
        var cart = await cartAppService.GetCartAsync(currentUser.UserId, cancellationToken);
        return Ok(WithAbsoluteImageUrls(cart));
    }

    [HttpPost("items")]
    public async Task<ActionResult<CartDto>> AddItem(AddCartItemRequest request, CancellationToken cancellationToken)
    {
        var cart = await cartAppService.AddItemAsync(currentUser.UserId, request, cancellationToken);
        return Ok(WithAbsoluteImageUrls(cart));
    }

    [HttpPut("items/{productId:guid}")]
    public async Task<ActionResult<CartDto>> UpdateItem(Guid productId, UpdateCartItemRequest request, CancellationToken cancellationToken)
    {
        var cart = await cartAppService.UpdateItemAsync(currentUser.UserId, productId, request, cancellationToken);
        return Ok(WithAbsoluteImageUrls(cart));
    }

    [HttpDelete("items/{productId:guid}")]
    public async Task<ActionResult<CartDto>> RemoveItem(Guid productId, CancellationToken cancellationToken)
    {
        var cart = await cartAppService.RemoveItemAsync(currentUser.UserId, productId, cancellationToken);
        return Ok(WithAbsoluteImageUrls(cart));
    }

    [HttpDelete]
    public async Task<IActionResult> Clear(CancellationToken cancellationToken)
    {
        await cartAppService.ClearCartAsync(currentUser.UserId, cancellationToken);
        return NoContent();
    }

    /// <summary>Converts the caller's current cart into a cash-on-pickup or unpaid-hold order, then empties the cart. Use POST /checkout/sessions to pay online via Monime instead.</summary>
    [HttpPost("checkout")]
    public async Task<ActionResult<OrderDto>> Checkout(CartCheckoutRequest request, CancellationToken cancellationToken)
    {
        var order = await cartAppService.CheckoutAsync(currentUser.UserId, request, cancellationToken);
        return Ok(order);
    }

    private CartDto WithAbsoluteImageUrls(CartDto cart)
    {
        return cart with
        {
            Items = cart.Items
                .Select(item => item with { ImageUrl = UploadUrlHelper.ToAbsolute(Request, item.ImageUrl) })
                .ToList()
        };
    }
}
