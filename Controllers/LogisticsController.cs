using AgrismartAPI.Application.Logistics;
using AgrismartAPI.Domain.Logistics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("logistics")]
[Authorize]
public class LogisticsController(DeliveryEstimateService deliveryEstimateService) : ControllerBase
{
    /// <summary>District names to offer when a buyer chooses where an order goes.</summary>
    [HttpGet("districts")]
    [AllowAnonymous]
    public ActionResult<IReadOnlyList<string>> GetDistricts() =>
        Ok(SierraLeoneDistricts.All.Select(district => district.Name).ToList());

    /// <summary>A rough delivery cost between two districts. It is not added to the order total.</summary>
    [HttpPost("delivery-estimate")]
    public ActionResult<DeliveryEstimateDto> Estimate(DeliveryEstimateRequest request) =>
        Ok(deliveryEstimateService.Estimate(request));
}
