using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Fertilizer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>
/// "Fertilizer recommendation and calculator" feature: how much fertilizer to buy and apply for a crop,
/// area, soil condition and target yield. Reference data and the stateless calculator are open to
/// anonymous callers; saved history requires sign-in; reference-data edits are admin-only.
/// </summary>
[ApiController]
[Route("fertilizer")]
[Tags("Fertilizer recommendation and calculator")]
[Produces("application/json")]
public class FertilizerController(FertilizerAppService fertilizerAppService) : ControllerBase
{
    // ---------- Crops ----------

    /// <summary>Lists crops with their nutrient requirements and application schedules.</summary>
    /// <response code="200">All crops, ordered by name.</response>
    [HttpGet("crops")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FertilizerCropDto>>> GetCrops(CancellationToken cancellationToken)
    {
        var crops = await fertilizerAppService.GetCropsAsync(cancellationToken);
        return Ok(crops);
    }

    /// <summary>Gets one crop's nutrient data.</summary>
    /// <param name="id">Crop id, e.g. "maize".</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The crop.</response>
    /// <response code="404">No crop with that id.</response>
    [HttpGet("crops/{id}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FertilizerCropDto>> GetCrop(string id, CancellationToken cancellationToken)
    {
        var crop = await fertilizerAppService.GetCropAsync(id, cancellationToken);
        return Ok(crop);
    }

    /// <summary>Adds a crop (admin only).</summary>
    /// <remarks>Each nutrient's schedule percentages must add up to 100.</remarks>
    /// <response code="201">Created.</response>
    /// <response code="400">Invalid values.</response>
    /// <response code="409">A crop with that id already exists.</response>
    [HttpPost("crops")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FertilizerCropDto>> CreateCrop(FertilizerCropRequest request, CancellationToken cancellationToken)
    {
        var crop = await fertilizerAppService.CreateCropAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetCrop), new { id = crop.Id }, crop);
    }

    /// <summary>Updates a crop's nutrient data and schedule (admin only). The id in the body is ignored.</summary>
    /// <param name="id">Crop id, e.g. "maize".</param>
    /// <param name="request">New values.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid values.</response>
    /// <response code="404">No crop with that id.</response>
    [HttpPut("crops/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FertilizerCropDto>> UpdateCrop(string id, FertilizerCropRequest request, CancellationToken cancellationToken)
    {
        var crop = await fertilizerAppService.UpdateCropAsync(id, request, cancellationToken);
        return Ok(crop);
    }

    // ---------- Products ----------

    /// <summary>Lists fertilizer products with their analysis, bag size and price.</summary>
    /// <response code="200">All products, ordered by name.</response>
    [HttpGet("products")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FertilizerProductDto>>> GetProducts(CancellationToken cancellationToken)
    {
        var products = await fertilizerAppService.GetProductsAsync(cancellationToken);
        return Ok(products);
    }

    /// <summary>Gets one fertilizer product.</summary>
    /// <param name="id">Product id, e.g. "npk-15-15-15".</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The product.</response>
    /// <response code="404">No product with that id.</response>
    [HttpGet("products/{id}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FertilizerProductDto>> GetProduct(string id, CancellationToken cancellationToken)
    {
        var product = await fertilizerAppService.GetProductAsync(id, cancellationToken);
        return Ok(product);
    }

    /// <summary>Adds a fertilizer product (admin only).</summary>
    /// <response code="201">Created.</response>
    /// <response code="400">Invalid values.</response>
    /// <response code="409">A product with that id already exists.</response>
    [HttpPost("products")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<FertilizerProductDto>> CreateProduct(FertilizerProductRequest request, CancellationToken cancellationToken)
    {
        var product = await fertilizerAppService.CreateProductAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    /// <summary>Updates a fertilizer product, e.g. its price per bag (admin only). The id in the body is ignored.</summary>
    /// <param name="id">Product id, e.g. "urea".</param>
    /// <param name="request">New values. Send pricePerBag: null to clear the price.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">Updated.</response>
    /// <response code="400">Invalid values.</response>
    /// <response code="404">No product with that id.</response>
    [HttpPut("products/{id}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FertilizerProductDto>> UpdateProduct(string id, FertilizerProductRequest request, CancellationToken cancellationToken)
    {
        var product = await fertilizerAppService.UpdateProductAsync(id, request, cancellationToken);
        return Ok(product);
    }

    // ---------- Calculation ----------

    /// <summary>Calculates a fertilizer plan without saving it.</summary>
    /// <remarks>
    /// Give the soil either as <c>soil.fertility</c> (simple mode) or as soil-test values
    /// (total N %, Bray-1 P mg/kg, exchangeable K cmol/kg). Leave <c>fertilizerProductIds</c> empty to
    /// let the API choose products. Validation errors return 400 with field-level <c>errors</c>.
    /// </remarks>
    /// <response code="200">The plan: quantities, bags, schedule, cost, warnings and notes.</response>
    /// <response code="400">Invalid input, unknown crop or unknown product.</response>
    [HttpPost("calculate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FertilizerCalculationResponseDto>> Calculate(
        FertilizerCalculationRequest request, CancellationToken cancellationToken)
    {
        var result = await fertilizerAppService.CalculateAsync(request, cancellationToken);
        return Ok(result);
    }

    // ---------- Saved recommendations ----------

    /// <summary>Calculates a fertilizer plan and saves it to the signed-in user's history.</summary>
    /// <remarks>Same body as POST /fertilizer/calculate; <c>plotLabel</c> is stored for filtering.</remarks>
    /// <response code="201">Saved; returns the stored inputs and result.</response>
    /// <response code="400">Invalid input.</response>
    /// <response code="401">Not signed in.</response>
    [HttpPost("recommendations")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<FertilizerRecommendationDto>> SaveRecommendation(
        FertilizerCalculationRequest request, CancellationToken cancellationToken)
    {
        var recommendation = await fertilizerAppService.SaveRecommendationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetRecommendation), new { id = recommendation.Id }, recommendation);
    }

    /// <summary>Lists saved recommendations, newest first (paginated).</summary>
    /// <param name="cropId">Only this crop, e.g. "maize".</param>
    /// <param name="plotLabel">Only this plot label (case-insensitive exact match).</param>
    /// <param name="userId">Admins only: a farmer's user id. Omit to see all farmers (admin) or yourself.</param>
    /// <param name="page">1-based page number (default 1).</param>
    /// <param name="pageSize">Items per page, 1-100 (default 20).</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">One page of summaries.</response>
    /// <response code="400">Invalid paging values.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="403">A non-admin asked for another user's history.</response>
    [HttpGet("recommendations")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<FertilizerRecommendationSummaryDto>>> GetRecommendations(
        [FromQuery] string? cropId,
        [FromQuery] string? plotLabel,
        [FromQuery] Guid? userId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var query = new FertilizerRecommendationQuery(cropId, plotLabel, userId, page ?? 1, pageSize ?? 20);
        var result = await fertilizerAppService.GetRecommendationsAsync(query, cancellationToken);
        return Ok(result);
    }

    /// <summary>Gets one saved recommendation with its full inputs and result.</summary>
    /// <param name="id">Recommendation id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="200">The recommendation.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Not found, or it belongs to another user.</response>
    [HttpGet("recommendations/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FertilizerRecommendationDto>> GetRecommendation(Guid id, CancellationToken cancellationToken)
    {
        var recommendation = await fertilizerAppService.GetRecommendationAsync(id, cancellationToken);
        return Ok(recommendation);
    }

    /// <summary>Deletes a saved recommendation.</summary>
    /// <param name="id">Recommendation id.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    /// <response code="204">Deleted.</response>
    /// <response code="401">Not signed in.</response>
    /// <response code="404">Not found, or it belongs to another user.</response>
    [HttpDelete("recommendations/{id:guid}")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteRecommendation(Guid id, CancellationToken cancellationToken)
    {
        await fertilizerAppService.DeleteRecommendationAsync(id, cancellationToken);
        return NoContent();
    }
}
