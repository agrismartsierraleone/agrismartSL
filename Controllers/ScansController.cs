using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Scans;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("scans")]
//[Authorize]
public class ScansController(ScanAppService scanAppService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlantScanDto>>> GetAll(CancellationToken cancellationToken)
    {
        var scans = await scanAppService.GetHistoryAsync(currentUser.UserId, cancellationToken);
        return Ok(scans);
    }

    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<PlantScanDto>> Create(
        IFormFile image,
        [FromForm] double? latitude,
        [FromForm] double? longitude,
        CancellationToken cancellationToken)
    {
        if (image.Length == 0)
        {
            return BadRequest(new { message = "An image file is required." });
        }

        if (latitude.HasValue != longitude.HasValue)
        {
            return BadRequest(new { message = "latitude and longitude must be provided together." });
        }

        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return BadRequest(new { message = "latitude must be between -90 and 90, and longitude between -180 and 180." });
        }

        await using var stream = image.OpenReadStream();
        var scan = await scanAppService.CreateAsync(
            currentUser.UserId,
            stream,
            image.FileName,
            image.ContentType ?? "application/octet-stream",
            latitude,
            longitude,
            cancellationToken);
        return Ok(scan);
    }
}
