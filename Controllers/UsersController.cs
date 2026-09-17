using AgrismartAPI.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("users")]
[Authorize(Roles = "Admin")]
public class UsersController(UserAppService userAppService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserSummaryDto>>> GetAll(CancellationToken cancellationToken)
    {
        var users = await userAppService.GetAllAsync(cancellationToken);
        return Ok(users);
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserSummaryDto>> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await userAppService.ChangeRoleAsync(id, request, cancellationToken);
        return Ok(user);
    }
}
