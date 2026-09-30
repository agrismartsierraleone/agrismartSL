using AgrismartAPI.Application.Assistant;
using AgrismartAPI.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.RateLimiting;

namespace AgrismartAPI.Controllers;

/// <summary>
/// "AgriSmart AI Assistant" feature: farmers ask farming questions in text and get answers personalized
/// with their farm data, weather, fertilizer calculator and harvest prediction. Controlled by the
/// "Features:AiAssistant" flag; when it is off, chat/report return 404 and status returns enabled=false.
/// </summary>
[ApiController]
[Route("api/assistant")]
[Tags("AI assistant")]
[Produces("application/json")]
[Authorize]
[EnableRateLimiting(AssistantOptions.RateLimitPolicy)]
public class AssistantController(IConfiguration configuration, ILogger<AssistantController> logger) : ControllerBase
{
    private const string UnavailableMessage = "The assistant is unavailable right now. Please try again later.";

    /// <summary>Whether the assistant is switched on.</summary>
    /// <response code="200">{ "enabled": true } or { "enabled": false }.</response>
    [HttpGet("status")]
    [AllowAnonymous]
    [DisableRateLimiting]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<AssistantStatusResponse> GetStatus() =>
        Ok(new AssistantStatusResponse(configuration.GetValue<bool>(AssistantOptions.FeatureFlagKey)));

    /// <summary>Asks the assistant a question.</summary>
    /// <remarks>Send conversationId: null to start a new conversation, or the id from a previous reply to continue it.</remarks>
    /// <response code="200">The assistant's reply.</response>
    /// <response code="400">message missing or longer than 1000 characters.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Assistant disabled, or the conversation does not exist or belongs to someone else.</response>
    /// <response code="429">Daily assistant limit reached.</response>
    /// <response code="503">The AI provider is unavailable.</response>
    [HttpPost("chat")]
    [AssistantEnabled]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<AssistantChatResponse>> Chat(
        AssistantChatRequest request, [FromServices] AssistantService assistantService, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await assistantService.ChatAsync(request, cancellationToken));
        }
        catch (AssistantUnavailableException)
        {
            // Already logged with details by AssistantService.
            return Unavailable();
        }
        catch (Exception exception) when (exception is not (ValidationException or NotFoundException
                                              or UnauthorizedAccessException or OperationCanceledException))
        {
            // Anything else inside the assistant (e.g. storage) is also a 503, never an unhandled 500.
            logger.LogError(exception, "Assistant chat failed unexpectedly");
            return Unavailable();
        }
    }

    /// <summary>Reports an AI-generated reply as wrong, harmful or inappropriate.</summary>
    /// <response code="204">Report saved.</response>
    /// <response code="400">reason missing or too long.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Assistant disabled, or the message was not found in one of your conversations.</response>
    [HttpPost("report")]
    [AssistantEnabled]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Report(
        AssistantReportRequest request, [FromServices] AssistantService assistantService, CancellationToken cancellationToken)
    {
        await assistantService.ReportAsync(request, cancellationToken);
        return NoContent();
    }

    private ObjectResult Unavailable()
    {
        logger.LogWarning("Assistant returned 503 to {Path}", HttpContext.Request.Path);
        return StatusCode(StatusCodes.Status503ServiceUnavailable, new { message = UnavailableMessage });
    }
}

/// <summary>
/// Returns 404 when "Features:AiAssistant" is false. Runs as a resource filter, i.e. before model binding
/// and [FromServices] resolution, so the unregistered assistant services are never requested.
/// </summary>
public sealed class AssistantEnabledAttribute : Attribute, IResourceFilter
{
    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var configuration = context.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        if (!configuration.GetValue<bool>(AssistantOptions.FeatureFlagKey))
        {
            context.Result = new NotFoundObjectResult(new { enabled = false });
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }
}
