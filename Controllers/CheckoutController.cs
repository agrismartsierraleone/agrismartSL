using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Checkout;
using AgrismartAPI.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>Path 1 of the three checkout flows — pay online via Monime (card/bank/momo, incl. Orange Money) before delivery. See MONIME-INTEGRATION-README.md.</summary>
[ApiController]
[Route("checkout")]
[Authorize(Roles = "Buyer")]
public class CheckoutController(CheckoutAppService checkoutAppService, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>Places an order from the caller's cart and creates a Monime hosted-checkout session for it. Redirect the browser to the returned redirectUrl.</summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<CheckoutSessionDto>> CreateSession(CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var session = await checkoutAppService.CreateSessionAsync(currentUser.UserId, request, cancellationToken);
            return Ok(session);
        }
        catch (MonimeApiException ex)
        {
            return MapMonimeError(ex);
        }
    }

    /// <summary>Polls the caller's own checkout session for its current status while waiting for the redirect to complete.</summary>
    [HttpGet("sessions/{id:guid}")]
    public async Task<ActionResult<CheckoutSessionDto>> GetSession(Guid id, CancellationToken cancellationToken)
    {
        var session = await checkoutAppService.GetSessionAsync(currentUser.UserId, id, cancellationToken);
        return Ok(session);
    }

    private ActionResult MapMonimeError(MonimeApiException ex) =>
        ex.StatusCode is >= 400 and < 500
            ? BadRequest(new { message = ex.Message })
            : StatusCode(StatusCodes.Status502BadGateway, new { message = ex.Message });
}
