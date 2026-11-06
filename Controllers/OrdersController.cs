using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("orders")]
[Authorize]
public class OrdersController(OrderAppService orderAppService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = "Buyer")]
    public async Task<ActionResult<OrderDto>> Create(CreateOrderRequest request, CancellationToken cancellationToken)
    {
        var order = await orderAppService.CreateOrderAsync(currentUser.UserId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { orderId = order.Id }, order);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<OrderDto>>> GetMine(CancellationToken cancellationToken)
    {
        var orders = await orderAppService.GetOrdersForUserAsync(currentUser.UserId, cancellationToken);
        return Ok(orders);
    }

    [HttpGet("{orderId:guid}")]
    public async Task<ActionResult<OrderWithCustomerDto>> GetById(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orderAppService.GetOrderAsync(currentUser.UserId, orderId, cancellationToken);
        return Ok(order);
    }

    /// <summary>Every order across every customer, including buyer details and the seller of each item.</summary>
    [HttpGet("all")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<OrderWithCustomerDto>>> GetAll(CancellationToken cancellationToken)
    {
        var orders = await orderAppService.GetAllOrdersAsync(cancellationToken);
        return Ok(orders);
    }

    /// <summary>Moves an order to Confirmed, Shipped or Delivered. Steps cannot be undone.</summary>
    [HttpPut("{orderId:guid}/delivery-status")]
    [Authorize(Roles = "Seller,Admin")]
    public async Task<ActionResult<OrderDto>> UpdateDeliveryStatus(Guid orderId, UpdateDeliveryStatusRequest request, CancellationToken cancellationToken)
    {
        var order = await orderAppService.UpdateDeliveryStatusAsync(orderId, request, cancellationToken);
        return Ok(order);
    }

    /// <summary>Recent orders worth a second look. These are flags for review, not proof of fraud.</summary>
    [HttpGet("alerts")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<OrderAlertDto>>> GetAlerts([FromServices] OrderAlertService orderAlertService, CancellationToken cancellationToken)
    {
        var alerts = await orderAlertService.GetAlertsAsync(cancellationToken);
        return Ok(alerts);
    }

    /// <summary>Orders containing at least one item sold by the calling seller, scoped to just that seller's items.</summary>
    [HttpGet("mine-as-seller")]
    [Authorize(Roles = "Seller")]
    public async Task<ActionResult<IReadOnlyList<OrderForSellerDto>>> GetMineAsSeller(CancellationToken cancellationToken)
    {
        var orders = await orderAppService.GetOrdersForSellerAsync(currentUser.UserId, cancellationToken);
        return Ok(orders);
    }
}
