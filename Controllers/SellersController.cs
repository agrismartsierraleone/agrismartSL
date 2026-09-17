using AgrismartAPI.Application.Market;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("sellers")]
[Authorize]
public class SellersController(SellerAppService sellerAppService) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<SellerDto>>> GetAll(CancellationToken cancellationToken)
    {
        var sellers = await sellerAppService.GetAllAsync(cancellationToken);
        return Ok(sellers);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<SellerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var seller = await sellerAppService.GetByIdAsync(id, cancellationToken);
        return Ok(seller);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<SellerDto>> Create(CreateSellerRequest request, CancellationToken cancellationToken)
    {
        var seller = await sellerAppService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = seller.Id }, seller);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Seller,Admin")]
    public async Task<ActionResult<SellerDto>> Update(Guid id, UpdateSellerRequest request, CancellationToken cancellationToken)
    {
        var seller = await sellerAppService.UpdateAsync(id, request, cancellationToken);
        return Ok(seller);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Seller,Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await sellerAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
