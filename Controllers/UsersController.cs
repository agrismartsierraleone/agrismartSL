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

    /// <summary>Admin-only: creates a seller or buyer account directly (bypasses public self-registration).</summary>
    [HttpPost]
    public async Task<ActionResult<UserSummaryDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await userAppService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { }, user);
    }

    [HttpPut("{id:guid}/role")]
    public async Task<ActionResult<UserSummaryDto>> ChangeRole(Guid id, ChangeUserRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await userAppService.ChangeRoleAsync(id, request, cancellationToken);
        return Ok(user);
    }

    /// <summary>Admin-only: updates a user's name, email and/or password. Notifies the user by email of any change.</summary>
    [HttpPut("{id:guid}/credentials")]
    public async Task<ActionResult<UserSummaryDto>> UpdateCredentials(Guid id, UpdateUserCredentialsRequest request, CancellationToken cancellationToken)
    {
        var user = await userAppService.UpdateCredentialsAsync(id, request, cancellationToken);
        return Ok(user);
    }

    /// <summary>Admin-only: permanently deletes a user account. Notifies the user by email.</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await userAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
