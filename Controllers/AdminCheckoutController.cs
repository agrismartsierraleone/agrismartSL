using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Checkout;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>
/// Admin-only payment reconciliation across all three checkout flows, plus raw Monime session/audit
/// visibility for support. See MONIME-INTEGRATION-README.md and README.admin.md.
/// </summary>
[ApiController]
[Route("admin/checkout")]
[Authorize(Roles = "Admin")]
public class AdminCheckoutController(CheckoutAppService checkoutAppService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Every order's payment state — filter by paymentStatus (pending/confirmed/paid) and/or paymentMethod to build the reconciliation grid.</summary>
    [HttpGet("transactions")]
    public async Task<ActionResult<IReadOnlyList<OrderPaymentDto>>> GetTransactions(
        [FromQuery] PaymentStatus? paymentStatus, [FromQuery] PaymentMethod? paymentMethod, CancellationToken cancellationToken)
    {
        var transactions = await checkoutAppService.GetTransactionsAsync(paymentStatus, paymentMethod, cancellationToken);
        return Ok(transactions);
    }

    /// <summary>Confirms payment was received (e.g. cash handed over at pickup) — Pending → Confirmed.</summary>
    [HttpPost("transactions/{orderId:guid}/confirm")]
    public async Task<ActionResult<OrderPaymentDto>> ConfirmPayment(Guid orderId, CancellationToken cancellationToken)
    {
        var transaction = await checkoutAppService.ConfirmPaymentAsync(currentUser.UserId, orderId, cancellationToken);
        return Ok(transaction);
    }

    /// <summary>Marks an order as fully paid/reconciled — the final step for cash-on-pickup/unpaid-hold orders.</summary>
    [HttpPost("transactions/{orderId:guid}/paid")]
    public async Task<ActionResult<OrderPaymentDto>> MarkPaid(Guid orderId, CancellationToken cancellationToken)
    {
        var transaction = await checkoutAppService.MarkPaidAsync(currentUser.UserId, orderId, cancellationToken);
        return Ok(transaction);
    }

    /// <summary>Monime-only: re-fetches the session status from Monime and reconciles the order against it.</summary>
    [HttpPost("transactions/{orderId:guid}/sync")]
    public async Task<ActionResult<OrderPaymentDto>> SyncMonimeSession(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await checkoutAppService.SyncMonimeSessionAsync(orderId, cancellationToken);
            return Ok(transaction);
        }
        catch (MonimeApiException ex)
        {
            return ex.StatusCode is >= 400 and < 500
                ? BadRequest(new { message = ex.Message })
                : StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>Every Monime checkout session ever created — for support/troubleshooting ("what happened to this payment").</summary>
    [HttpGet("sessions")]
    public async Task<ActionResult<IReadOnlyList<CheckoutSessionAdminDto>>> GetSessions(CancellationToken cancellationToken)
    {
        var sessions = await checkoutAppService.GetSessionsAdminAsync(cancellationToken);
        return Ok(sessions);
    }

    /// <summary>One session's full detail, including the raw Monime payload and every create/reconcile attempt against it.</summary>
    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<CheckoutSessionAdminDto>> GetSession(Guid id, CancellationToken cancellationToken)
    {
        var session = await checkoutAppService.GetSessionAdminAsync(id, cancellationToken);
        return Ok(session);
    }
}
