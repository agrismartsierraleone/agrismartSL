using AgrismartAPI.Application.Diagnoses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("diagnoses")]
[Authorize]
public class DiagnosesController(DiagnosisAppService diagnosisAppService) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<DiagnosisResultDto>> Diagnose(
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
        var result = await diagnosisAppService.DiagnoseAsync(stream, image.FileName, latitude, longitude, cancellationToken);
        return Ok(result);
    }
}
