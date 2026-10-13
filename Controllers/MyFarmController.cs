using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.MyFarm;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>
/// "My Farm": the farm management screen. The farm itself is created and edited with /farms (FarmsController);
/// these endpoints serve the screen's tabs: Crops (overview), Map, Activities and Analytics, plus Crop Health.
/// Only the signed-in farmer's farms are visible; another farmer's farm id returns 404.
/// </summary>
[ApiController]
[Route("farms")]
[Tags("My farm")]
[Produces("application/json")]
[Authorize]
public class MyFarmController(MyFarmAppService myFarm, ICurrentUserService currentUser) : ControllerBase
{
    public const long MaxImageBytes = 10 * 1024 * 1024;

    /// <summary>Lookup lists for the My Farm forms: districts, crops, activity types, area units.</summary>
    /// <response code="200">The lists.</response>
    [HttpGet("options")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<MyFarmOptionsDto>> GetOptions(CancellationToken cancellationToken) =>
        Ok(await myFarm.GetOptionsAsync(cancellationToken));

    /// <summary>Everything on the "Crops" tab in one call: header, crop cards, crop calendar, upcoming activities, crop health.</summary>
    /// <param name="farmId">Farm id from GET /farms.</param>
    /// <param name="calendarCropId">Crop card id to show in the Crop Calendar. Defaults to the first crop in the field.</param>
    /// <param name="activitiesLimit">How many upcoming activities to return (1-20, default 3).</param>
    /// <response code="200">The overview.</response>
    /// <response code="404">Farm (or calendarCropId) not found.</response>
    [HttpGet("{farmId:guid}/overview")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmOverviewDto>> GetOverview(
        Guid farmId, [FromQuery] Guid? calendarCropId, [FromQuery] int activitiesLimit = 3, CancellationToken cancellationToken = default) =>
        Ok(await myFarm.GetOverviewAsync(currentUser.UserId, farmId, calendarCropId, activitiesLimit, cancellationToken));

    /// <summary>The "Map" tab: farm pin and outline, and each crop plot's pin and outline.</summary>
    /// <response code="200">The map data.</response>
    /// <response code="404">Farm not found.</response>
    [HttpGet("{farmId:guid}/map")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmMapDto>> GetMap(Guid farmId, CancellationToken cancellationToken) =>
        Ok(await myFarm.GetMapAsync(currentUser.UserId, farmId, cancellationToken));

    /// <summary>Sets the farm pin and outline (3-200 points). Plot pins/outlines are set on each crop.</summary>
    /// <response code="200">Updated map data.</response>
    /// <response code="400">Invalid coordinates.</response>
    /// <response code="404">Farm not found.</response>
    [HttpPut("{farmId:guid}/map")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmMapDto>> UpdateMap(Guid farmId, FarmMapRequest request, CancellationToken cancellationToken) =>
        Ok(await myFarm.UpdateMapAsync(currentUser.UserId, farmId, request, cancellationToken));

    /// <summary>Crop Health: the latest scan and this farm's scan history ("View History").</summary>
    /// <response code="200">Health data.</response>
    /// <response code="404">Farm not found.</response>
    [HttpGet("{farmId:guid}/health")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CropHealthDto>> GetHealth(Guid farmId, CancellationToken cancellationToken) =>
        Ok(await myFarm.GetHealthAsync(currentUser.UserId, farmId, cancellationToken));

    /// <summary>"Scan Plant": diagnoses a photo and files it under this farm (and optionally one crop).</summary>
    /// <remarks>multipart/form-data with <c>image</c> (JPEG/PNG, up to 10 MB) and optional <c>farmCropId</c>.</remarks>
    /// <response code="200">The diagnosis.</response>
    /// <response code="400">Missing or invalid image.</response>
    /// <response code="404">Farm or crop not found.</response>
    [HttpPost("{farmId:guid}/health/scans")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(MaxImageBytes + 1024 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CropHealthScanDto>> Scan(
        Guid farmId, IFormFile? image, [FromForm] Guid? farmCropId, CancellationToken cancellationToken)
    {
        if (ImageError(image) is { } error)
        {
            return BadRequest(new { message = error });
        }

        await using var stream = image!.OpenReadStream();
        var file = new UploadedFile(stream, image.FileName, image.ContentType);
        return Ok(await myFarm.ScanAsync(currentUser.UserId, farmId, farmCropId, file, cancellationToken));
    }

    /// <summary>The "Analytics" tab: land use, crop status counts, activity completion and spend, yields and crop health.</summary>
    /// <response code="200">The analytics.</response>
    /// <response code="404">Farm not found.</response>
    [HttpGet("{farmId:guid}/analytics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FarmAnalyticsDto>> GetAnalytics(Guid farmId, CancellationToken cancellationToken) =>
        Ok(await myFarm.GetAnalyticsAsync(currentUser.UserId, farmId, cancellationToken));

    /// <summary>Null when the upload is a usable image, otherwise the message to return.</summary>
    internal static string? ImageError(IFormFile? image)
    {
        if (image is null || image.Length == 0)
        {
            return "An image file is required.";
        }

        if (image.Length > MaxImageBytes)
        {
            return "The image must be 10 MB or smaller.";
        }

        return image.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) == true
            ? null
            : "The file must be an image (JPEG or PNG).";
    }
}
