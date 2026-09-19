using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Checkout;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>
/// Payment reconciliation across all three checkout flows. An Admin can act on every seller's row on
/// every order; a Seller can act only on their own row within orders containing at least one of their
/// own products (same order-level scoping as <c>GET /orders/mine-as-seller</c>) — a Seller can never
/// confirm or reconcile another seller's products, even within the same multi-vendor order. Raw Monime
/// session/audit visibility stays Admin-only. See MONIME-INTEGRATION-README.md and README.admin.md.
/// </summary>
[ApiController]
[Route("checkout/reconciliation")]
[Authorize]
public class CheckoutReconciliationController(CheckoutAppService checkoutAppService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Every order's payment state the caller is allowed to see — filter by paymentStatus (pending/confirmed/paid) and/or paymentMethod to build the reconciliation grid.</summary>
    [HttpGet("transactions")]
    [Authorize(Roles = "Admin,Seller")]
    public async Task<ActionResult<IReadOnlyList<OrderPaymentDto>>> GetTransactions(
        [FromQuery] PaymentStatus? paymentStatus, [FromQuery] PaymentMethod? paymentMethod, CancellationToken cancellationToken)
    {
        var transactions = await checkoutAppService.GetTransactionsAsync(currentUser.UserId, currentUser.Role, paymentStatus, paymentMethod, cancellationToken);
        return Ok(transactions);
    }

    /// <summary>
    /// Confirms payment was received (e.g. cash handed over at pickup) — Pending → Confirmed, for one
    /// seller's own products within the order. A Seller's own products are always used, regardless of
    /// what (if anything) is passed as <paramref name="sellerId"/>. An Admin must pass it when the
    /// order spans more than one seller.
    /// </summary>
    [HttpPost("transactions/{orderId:guid}/confirm")]
    [Authorize(Roles = "Admin,Seller")]
    public async Task<ActionResult<OrderPaymentDto>> ConfirmPayment(Guid orderId, [FromQuery] Guid? sellerId, CancellationToken cancellationToken)
    {
        var transaction = await checkoutAppService.ConfirmPaymentAsync(currentUser.UserId, currentUser.Role, orderId, sellerId, cancellationToken);
        return Ok(transaction);
    }

    /// <summary>
    /// Marks one seller's own products within the order as fully paid/reconciled — the final step for
    /// cash-on-pickup/unpaid-hold orders. A Seller's own products are always used, regardless of what
    /// (if anything) is passed as <paramref name="sellerId"/>. An Admin must pass it when the order
    /// spans more than one seller.
    /// </summary>
    [HttpPost("transactions/{orderId:guid}/paid")]
    [Authorize(Roles = "Admin,Seller")]
    public async Task<ActionResult<OrderPaymentDto>> MarkPaid(Guid orderId, [FromQuery] Guid? sellerId, CancellationToken cancellationToken)
    {
        var transaction = await checkoutAppService.MarkPaidAsync(currentUser.UserId, currentUser.Role, orderId, sellerId, cancellationToken);
        return Ok(transaction);
    }

    /// <summary>Monime-only: re-fetches the session status from Monime and reconciles the order against it.</summary>
    [HttpPost("transactions/{orderId:guid}/sync")]
    [Authorize(Roles = "Admin,Seller")]
    public async Task<ActionResult<OrderPaymentDto>> SyncMonimeSession(Guid orderId, CancellationToken cancellationToken)
    {
        try
        {
            var transaction = await checkoutAppService.SyncMonimeSessionAsync(currentUser.UserId, currentUser.Role, orderId, cancellationToken);
            return Ok(transaction);
        }
        catch (MonimeApiException ex)
        {
            return ex.StatusCode is >= 400 and < 500
                ? BadRequest(new { message = ex.Message })
                : StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
        }
    }

    /// <summary>Every Monime checkout session ever created — for support/troubleshooting ("what happened to this payment"). Admin-only: spans every seller's orders.</summary>
    [HttpGet("sessions")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<CheckoutSessionAdminDto>>> GetSessions(CancellationToken cancellationToken)
    {
        var sessions = await checkoutAppService.GetSessionsAdminAsync(cancellationToken);
        return Ok(sessions);
    }

    /// <summary>One session's full detail, including the raw Monime payload and every create/reconcile attempt against it. Admin-only.</summary>
    [HttpGet("sessions/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<CheckoutSessionAdminDto>> GetSession(Guid id, CancellationToken cancellationToken)
    {
        var session = await checkoutAppService.GetSessionAdminAsync(id, cancellationToken);
        return Ok(session);
    }
}
