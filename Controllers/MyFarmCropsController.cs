using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.MyFarm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace AgrismartAPI.Controllers;

/// <summary>"My Crops" on the My Farm screen: crop cards, Add Crop, harvest, and the full crop calendar.</summary>
[ApiController]
[Route("farms/{farmId:guid}/crops")]
[Tags("My farm")]
[Produces("application/json")]
[Authorize]
public class MyFarmCropsController(MyFarmAppService myFarm, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>The farm's crop cards: in the field first, then planned, then harvested/failed.</summary>
    /// <param name="farmId">Farm id from GET /farms.</param>
    /// <param name="status">Optional filter: planned, growing, readyToHarvest, harvested or failed.</param>
    /// <response code="200">Crop cards.</response>
    /// <response code="404">Farm not found.</response>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<FarmCropCardDto>>> GetCrops(
        Guid farmId, [FromQuery] FarmCropDisplayStatus? status, CancellationToken cancellationToken) =>
        Ok(await myFarm.GetCropsAsync(currentUser.UserId, farmId, status, cancellationToken));

    /// <summary>One crop card.</summary>
    /// <response code="200">The crop.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpGet("{cropId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmCropCardDto>> GetCrop(Guid farmId, Guid cropId, CancellationToken cancellationToken) =>
        Ok(await myFarm.GetCropAsync(currentUser.UserId, farmId, cropId, cancellationToken));

    /// <summary>"Add Crop". With a planting date, the crop calendar's upcoming tasks are added as activities.</summary>
    /// <response code="201">Created; returns the crop card.</response>
    /// <response code="400">Invalid values, with field-level errors.</response>
    /// <response code="404">Farm not found.</response>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmCropCardDto>> AddCrop(Guid farmId, FarmCropUpsertRequest request, CancellationToken cancellationToken)
    {
        var crop = await myFarm.AddCropAsync(currentUser.UserId, farmId, request, cancellationToken);
        return CreatedAtAction(nameof(GetCrop), new { farmId, cropId = crop.Id }, crop);
    }

    /// <summary>Edits a crop. Every field is replaced (send the current values for fields you keep).</summary>
    /// <remarks>
    /// If cropId, plantingDate or expectedHarvestDate change (and generateActivities is true), open calendar tasks are
    /// replaced by a new plan. Tasks the farmer added or finished are kept.
    /// </remarks>
    /// <response code="200">Updated crop card.</response>
    /// <response code="400">Invalid values, with field-level errors.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpPut("{cropId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmCropCardDto>> UpdateCrop(
        Guid farmId, Guid cropId, FarmCropUpsertRequest request, CancellationToken cancellationToken) =>
        Ok(await myFarm.UpdateCropAsync(currentUser.UserId, farmId, cropId, request, cancellationToken));

    /// <summary>Deletes a crop and its open tasks. Completed tasks stay in the farm history.</summary>
    /// <response code="204">Deleted.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpDelete("{cropId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteCrop(Guid farmId, Guid cropId, CancellationToken cancellationToken)
    {
        await myFarm.DeleteCropAsync(currentUser.UserId, farmId, cropId, cancellationToken);
        return NoContent();
    }

    /// <summary>Uploads the farmer's own photo for the crop card (replaces the default crop image).</summary>
    /// <remarks>multipart/form-data with <c>image</c> (JPEG/PNG, up to 10 MB).</remarks>
    /// <response code="200">Updated crop card with imageUrl.</response>
    /// <response code="400">Missing or invalid image.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpPost("{cropId:guid}/image")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MyFarmController.MaxImageBytes + 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmCropCardDto>> UploadImage(Guid farmId, Guid cropId, IFormFile? image, CancellationToken cancellationToken)
    {
        if (MyFarmController.ImageError(image) is { } error)
        {
            return BadRequest(new { message = error });
        }

        await using var stream = image!.OpenReadStream();
        var file = new UploadedFile(stream, image.FileName, image.ContentType);
        return Ok(await myFarm.SetCropImageAsync(currentUser.UserId, farmId, cropId, file, cancellationToken));
    }

    /// <summary>Marks the crop harvested and optionally records the yield.</summary>
    /// <response code="200">Updated crop card.</response>
    /// <response code="400">Invalid values, with field-level errors.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpPost("{cropId:guid}/harvest")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmCropCardDto>> Harvest(
        Guid farmId, Guid cropId, [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] HarvestCropRequest? request, CancellationToken cancellationToken) =>
        Ok(await myFarm.HarvestCropAsync(currentUser.UserId, farmId, cropId, request ?? new HarvestCropRequest(), cancellationToken));

    /// <summary>Marks the crop failed, or puts a harvested/failed crop back in the field ("active").</summary>
    /// <response code="200">Updated crop card.</response>
    /// <response code="400">Invalid status.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpPut("{cropId:guid}/status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmCropCardDto>> SetStatus(
        Guid farmId, Guid cropId, CropStatusRequest request, CancellationToken cancellationToken) =>
        Ok(await myFarm.SetCropStatusAsync(currentUser.UserId, farmId, cropId, request, cancellationToken));

    /// <summary>"View Full Calendar": the six calendar stages and every task for this crop.</summary>
    /// <response code="200">The calendar.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpGet("{cropId:guid}/calendar")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CropCalendarDto>> GetCalendar(Guid farmId, Guid cropId, CancellationToken cancellationToken) =>
        Ok(await myFarm.GetCalendarAsync(currentUser.UserId, farmId, cropId, cancellationToken));
}
