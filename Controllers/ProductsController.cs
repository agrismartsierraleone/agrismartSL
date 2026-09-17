using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Market;
using AgrismartAPI.Controllers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("products")]
[Authorize]
public class ProductsController(MarketAppService marketAppService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<MarketProductDto>>> GetAll([FromQuery] Guid? categoryId, CancellationToken cancellationToken)
    {
        var products = await marketAppService.GetProductsAsync(categoryId, cancellationToken);
        return Ok(products.Select(WithAbsoluteImageUrl));
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<MarketProductDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var product = await marketAppService.GetProductAsync(id, cancellationToken);
        return Ok(WithAbsoluteImageUrl(product));
    }

    [HttpPost]
    [Authorize(Roles = "Seller,Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<MarketProductDto>> Create([FromForm] ProductForm form, CancellationToken cancellationToken)
    {
        var request = new CreateProductRequest(
            form.Name,
            form.CategoryId,
            form.Price,
            form.Unit,
            form.SellerId,
            form.Description,
            ToUploadedFile(form.Image));

        var product = await marketAppService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = product.Id }, WithAbsoluteImageUrl(product));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Seller,Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<MarketProductDto>> Update(Guid id, [FromForm] ProductForm form, CancellationToken cancellationToken)
    {
        var request = new UpdateProductRequest(
            form.Name,
            form.CategoryId,
            form.Price,
            form.Unit,
            form.SellerId,
            form.Description,
            ToUploadedFile(form.Image));

        var product = await marketAppService.UpdateAsync(id, request, cancellationToken);
        return Ok(WithAbsoluteImageUrl(product));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Seller,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await marketAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    private static UploadedFile? ToUploadedFile(IFormFile? file)
    {
        return file is null ? null : new UploadedFile(file.OpenReadStream(), file.FileName, file.ContentType);
    }

    private MarketProductDto WithAbsoluteImageUrl(MarketProductDto product)
    {
        return product with { ImageUrl = UploadUrlHelper.ToAbsolute(Request, product.ImageUrl) };
    }
}
