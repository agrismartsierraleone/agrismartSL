using AgrismartAPI.Application.Harvest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>Crop catalog backing the "Crops and harvest prediction" feature — see also HarvestPredictionsController.</summary>
[ApiController]
[Route("crops")]
[Tags("Crops and harvest prediction")]
[AllowAnonymous]
public class CropsController(HarvestAppService harvestAppService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CropSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var crops = await harvestAppService.GetCropsAsync(cancellationToken);
        return Ok(crops);
    }

    [HttpGet("{cropId}")]
    public async Task<ActionResult<CropSummaryDto>> GetOne(string cropId, CancellationToken cancellationToken)
    {
        var crop = await harvestAppService.GetCropAsync(cropId, cancellationToken);
        return Ok(crop);
    }
}
