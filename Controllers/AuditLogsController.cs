using AgrismartAPI.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

/// <summary>Admin-only activity trail: account creation, credential changes, role changes, seller changes, logins.</summary>
[ApiController]
[Route("audit-logs")]
[Authorize(Roles = "Admin")]
public class AuditLogsController(AuditLogAppService auditLogAppService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AuditLogDto>>> GetAll(CancellationToken cancellationToken)
    {
        var logs = await auditLogAppService.GetAllAsync(cancellationToken);
        return Ok(logs);
    }
}
