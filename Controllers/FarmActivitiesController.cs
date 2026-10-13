using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.MyFarm;
using AgrismartAPI.Domain.Farms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AgrismartAPI.Controllers;

/// <summary>The "Activities" tab and "Upcoming Activities" list of the My Farm screen.</summary>
[ApiController]
[Route("farms/{farmId:guid}/activities")]
[Tags("My farm")]
[Produces("application/json")]
[Authorize]
public class FarmActivitiesController(MyFarmAppService myFarm, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>The farm's activities: open ones first (soonest due first), then finished ones (latest first).</summary>
    /// <param name="farmId">Farm id from GET /farms.</param>
    /// <param name="status">pending, completed or skipped.</param>
    /// <param name="farmCropId">Only this crop's activities.</param>
    /// <param name="type">landPreparation, planting, fertilizing, weeding, pestControl, harvesting, irrigation, scouting or other.</param>
    /// <param name="from">Due on or after (yyyy-MM-dd).</param>
    /// <param name="to">Due on or before (yyyy-MM-dd).</param>
    /// <param name="page">1-based page.</param>
    /// <param name="pageSize">1-100, default 20.</param>
    /// <response code="200">One page of activities.</response>
    /// <response code="404">Farm not found.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<FarmActivityDto>>> GetActivities(
        Guid farmId,
        [FromQuery] FarmActivityStatus? status,
        [FromQuery] Guid? farmCropId,
        [FromQuery] FarmActivityType? type,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await myFarm.GetActivitiesAsync(currentUser.UserId, farmId, status, farmCropId, type, from, to, page, pageSize, cancellationToken));

    /// <summary>Adds a task, for the whole farm or one crop (farmCropId).</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Invalid values, with field-level errors.</response>
    /// <response code="404">Farm not found.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmActivityDto>> AddActivity(Guid farmId, FarmActivityRequest request, CancellationToken cancellationToken)
    {
        var activity = await myFarm.AddActivityAsync(currentUser.UserId, farmId, request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, activity);
    }

    /// <summary>Edits a task (all fields replaced). Its status is changed with complete/skip/reopen.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid values, with field-level errors.</response>
    /// <response code="404">Farm or activity not found.</response>
    [HttpPut("{activityId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmActivityDto>> UpdateActivity(
        Guid farmId, Guid activityId, FarmActivityRequest request, CancellationToken cancellationToken) =>
        Ok(await myFarm.UpdateActivityAsync(currentUser.UserId, farmId, activityId, request, cancellationToken));

    /// <summary>Marks a task done. The body is optional: completedOn (default today), actual cost, notes.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid values.</response>
    /// <response code="404">Farm or activity not found.</response>
    [HttpPost("{activityId:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmActivityDto>> Complete(
        Guid farmId, Guid activityId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] CompleteActivityRequest? request, CancellationToken cancellationToken) =>
        Ok(await myFarm.CompleteActivityAsync(currentUser.UserId, farmId, activityId, request, cancellationToken));

    /// <summary>Marks a task as not needed.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="404">Farm or activity not found.</response>
    [HttpPost("{activityId:guid}/skip")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmActivityDto>> Skip(Guid farmId, Guid activityId, CancellationToken cancellationToken) =>
        Ok(await myFarm.SkipActivityAsync(currentUser.UserId, farmId, activityId, cancellationToken));

    /// <summary>Puts a completed or skipped task back to pending.</summary>
    /// <response code="200">Updated.</response>
    /// <response code="404">Farm or activity not found.</response>
    [HttpPost("{activityId:guid}/reopen")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmActivityDto>> Reopen(Guid farmId, Guid activityId, CancellationToken cancellationToken) =>
        Ok(await myFarm.ReopenActivityAsync(currentUser.UserId, farmId, activityId, cancellationToken));

    /// <summary>Deletes a task.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Farm or activity not found.</response>
    [HttpDelete("{activityId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteActivity(Guid farmId, Guid activityId, CancellationToken cancellationToken)
    {
        await myFarm.DeleteActivityAsync(currentUser.UserId, farmId, activityId, cancellationToken);
        return NoContent();
    }
}
