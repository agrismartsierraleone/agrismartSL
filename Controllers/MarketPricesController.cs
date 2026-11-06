using AgrismartAPI.Application.Market;
using AgrismartAPI.Domain.Market;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>
/// Daily crop, fertilizer and seed prices by district. Reading is public, like the product catalog, so that a ticker refreshing in
/// the background never fails on an expired token and logs the user out.
/// </summary>
[ApiController]
[Route("market-prices")]
[Tags("Market prices")]
public class MarketPricesController(MarketPriceAppService marketPriceAppService) : ControllerBase
{
    /// <summary>The latest price of each item in one category, per district, with the change since the record before it. The category defaults to crop.</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<MarketPriceBoardDto>> GetBoard([FromQuery] MarketPriceCategory? category, [FromQuery] string? crop, [FromQuery] string? district, [FromQuery] DateOnly? date, CancellationToken cancellationToken) =>
        Ok(await marketPriceAppService.GetBoardAsync(category, crop, district, date, cancellationToken));

    /// <summary>The same board as a PDF to download. It takes the same filters.</summary>
    [HttpGet("pdf")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "application/pdf")]
    public async Task<IActionResult> DownloadBoardPdf([FromQuery] MarketPriceCategory? category, [FromQuery] string? crop, [FromQuery] string? district, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
    {
        var pdf = await marketPriceAppService.GetBoardPdfAsync(category, crop, district, date, cancellationToken);
        return File(pdf.Content, "application/pdf", pdf.FileName);
    }

    /// <summary>The newest prices for the scrolling ticker, in every category: from the last 7 days, at most 60.</summary>
    [HttpGet("ticker")]
    [AllowAnonymous]
    public async Task<ActionResult<MarketPriceBoardDto>> GetTicker(CancellationToken cancellationToken) =>
        Ok(await marketPriceAppService.GetTickerAsync(cancellationToken));

    /// <summary>One item's prices in one district, oldest first, for a chart. The category defaults to crop.</summary>
    [HttpGet("history")]
    [AllowAnonymous]
    public async Task<ActionResult<MarketPriceHistoryDto>> GetHistory([FromQuery] MarketPriceCategory? category, [FromQuery] string? crop, [FromQuery] string? district, [FromQuery] int days = 30, CancellationToken cancellationToken = default) =>
        Ok(await marketPriceAppService.GetHistoryAsync(category, crop, district, days, cancellationToken));

    /// <summary>Records up to 200 prices for one day, all or nothing. Sending a price again replaces it.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<RecordMarketPricesResultDto>> Record(RecordMarketPricesRequest request, CancellationToken cancellationToken) =>
        Ok(await marketPriceAppService.RecordAsync(request, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await marketPriceAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
