using AgrismartAPI.Application.AiTools;
using AgrismartAPI.Application.Assistant;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace AgrismartAPI.Controllers;

/// <summary>
/// AI helpers for trade, payments, logistics and jobs. They share the assistant's "Features:AiAssistant" flag
/// (404 when it is off) and its daily limit per user. A failed AI call returns 503.
/// </summary>
[ApiController]
[Route("api/ai")]
[Tags("AI tools")]
[Authorize]
[AssistantEnabled]
[EnableRateLimiting(AssistantOptions.RateLimitPolicy)]
public class AiToolsController(ILogger<AiToolsController> logger) : ControllerBase
{
    /// <summary>Drafts a product listing from notes and/or a photo. Nothing is saved.</summary>
    [HttpPost("listing-draft")]
    [Authorize(Roles = "Seller,Admin")]
    public async Task<ActionResult<ListingDraftDto>> DraftListing(
        [FromForm] string? notes, IFormFile? image, [FromServices] AiToolsService service, CancellationToken cancellationToken)
    {
        await using var stream = image?.OpenReadStream();
        var file = image is null ? null : new UploadedFile(stream!, image.FileName, image.ContentType);
        return await RunAsync(() => service.DraftListingAsync(notes, file, cancellationToken));
    }

    /// <summary>Suggests a price from similar listings and past sales. Returns no price when too few are comparable.</summary>
    [HttpPost("price-suggestion")]
    [Authorize(Roles = "Seller,Admin")]
    public Task<ActionResult<PriceSuggestionDto>> SuggestPrice(
        PriceSuggestionRequest request, [FromServices] AiToolsService service, CancellationToken cancellationToken) =>
        RunAsync(() => service.SuggestPriceAsync(request, cancellationToken));

    /// <summary>The calling seller's earnings, with a plain-language summary.</summary>
    [HttpGet("earnings-summary")]
    [Authorize(Roles = "Seller")]
    public Task<ActionResult<EarningsSummaryDto>> GetEarningsSummary(
        [FromServices] AiToolsService service, CancellationToken cancellationToken, [FromQuery] int days = 30) =>
        RunAsync(() => service.GetEarningsSummaryAsync(days, cancellationToken));

    /// <summary>Groups orders that are ready to send by destination district. Sellers see only their own items.</summary>
    [HttpGet("delivery-plan")]
    [Authorize(Roles = "Seller,Admin")]
    public Task<ActionResult<DeliveryPlanDto>> GetDeliveryPlan(
        [FromServices] AiToolsService service, CancellationToken cancellationToken, [FromQuery] string? fromDistrict = null) =>
        RunAsync(() => service.GetDeliveryPlanAsync(fromDistrict, cancellationToken));

    /// <summary>Turns a short note into a job post to review. Nothing is saved.</summary>
    [HttpPost("job-draft")]
    [Authorize(Roles = "Admin")]
    public Task<ActionResult<JobDraftDto>> DraftJob(
        JobDraftRequest request, [FromServices] AiToolsService service, CancellationToken cancellationToken) =>
        RunAsync(() => service.DraftJobAsync(request, cancellationToken));

    /// <summary>Turns a worker's answers into a profile to review. Nothing is saved.</summary>
    [HttpPost("worker-profile-draft")]
    public Task<ActionResult<WorkerProfileDraftDto>> DraftWorkerProfile(
        WorkerProfileDraftRequest request, [FromServices] AiToolsService service, CancellationToken cancellationToken) =>
        RunAsync(() => service.DraftWorkerProfileAsync(request, cancellationToken));

    /// <summary>Suggests available workers for a job. Poster or admin only.</summary>
    [HttpGet("jobs/{jobId:guid}/matches")]
    public Task<ActionResult<IReadOnlyList<WorkerMatchDto>>> GetWorkerMatches(
        Guid jobId, [FromServices] AiToolsService service, CancellationToken cancellationToken) =>
        RunAsync(() => service.GetWorkerMatchesAsync(jobId, cancellationToken));

    private async Task<ActionResult<T>> RunAsync<T>(Func<Task<T>> action)
    {
        try
        {
            return Ok(await action());
        }
        catch (AssistantUnavailableException)
        {
            // Already logged with details by AiToolsService.
            logger.LogWarning("AI tools returned 503 to {Path}", HttpContext.Request.Path);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = "The AI service is unavailable right now. Please try again later." });
        }
    }
}
