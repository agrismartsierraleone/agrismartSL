using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Harvest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>
/// "Crops and harvest prediction" feature: growing-degree-day harvest date prediction for a crop,
/// location and planting date. Open to anonymous callers (like the crop catalog); predictions made
/// by a signed-in user are additionally saved to their history — see GetHistory.
/// </summary>
[ApiController]
[Route("harvest")]
[Tags("Crops and harvest prediction")]
public class HarvestPredictionsController(HarvestAppService harvestAppService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpPost("predict")]
    [AllowAnonymous]
    public async Task<ActionResult<HarvestPredictionResponseDto>> Predict(
        HarvestPredictionRequest request, CancellationToken cancellationToken)
    {
        var prediction = await harvestAppService.PredictAsync(request, CurrentUserIdOrNull(), cancellationToken);
        return Ok(prediction);
    }

    /// <summary>Same prediction, as a GET with query parameters for quick manual testing.</summary>
    [HttpGet("predict")]
    [AllowAnonymous]
    public async Task<ActionResult<HarvestPredictionResponseDto>> PredictQuery(
        [FromQuery] string crop,
        [FromQuery] double lat,
        [FromQuery] double lon,
        [FromQuery] DateOnly planted,
        [FromQuery] bool? trace,
        [FromQuery] double? gddOverride,
        CancellationToken cancellationToken)
    {
        var request = new HarvestPredictionRequest(crop, lat, lon, planted, trace ?? false, gddOverride);
        var prediction = await harvestAppService.PredictAsync(request, CurrentUserIdOrNull(), cancellationToken);
        return Ok(prediction);
    }

    [HttpGet("history")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<HarvestPredictionResponseDto>>> GetHistory(CancellationToken cancellationToken)
    {
        var history = await harvestAppService.GetHistoryAsync(currentUser.UserId, cancellationToken);
        return Ok(history);
    }

    private Guid? CurrentUserIdOrNull() => currentUser.IsAuthenticated ? currentUser.UserId : null;
}
