using AgrismartAPI.Application.Jobs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("jobs")]
[Authorize]
public class JobsController(JobAppService jobAppService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<JobDto>>> GetOpen([FromQuery] string? district, [FromQuery] string? skill, CancellationToken cancellationToken) =>
        Ok(await jobAppService.GetOpenAsync(district, skill, cancellationToken));

    /// <summary>Admin only. Buyers and sellers can browse and apply.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<JobDto>> Create(CreateJobRequest request, CancellationToken cancellationToken)
    {
        var job = await jobAppService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<JobDto>> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await jobAppService.GetAsync(id, cancellationToken));

    /// <summary>Stops new applications. Poster or admin only.</summary>
    [HttpPost("{id:guid}/close")]
    public async Task<ActionResult<JobDto>> Close(Guid id, CancellationToken cancellationToken) =>
        Ok(await jobAppService.CloseAsync(id, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await jobAppService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Buyers and sellers only.</summary>
    [HttpPost("{id:guid}/apply")]
    [Authorize(Roles = "Buyer,Seller")]
    public async Task<IActionResult> Apply(Guid id, ApplyToJobRequest request, CancellationToken cancellationToken)
    {
        await jobAppService.ApplyAsync(id, request, cancellationToken);
        return NoContent();
    }

    /// <summary>People who applied, with their phone numbers. Poster or admin only.</summary>
    [HttpGet("{id:guid}/applicants")]
    public async Task<ActionResult<IReadOnlyList<JobApplicantDto>>> GetApplicants(Guid id, CancellationToken cancellationToken) =>
        Ok(await jobAppService.GetApplicantsAsync(id, cancellationToken));

    [HttpGet("worker-profile")]
    public async Task<ActionResult<WorkerProfileDto>> GetMyWorkerProfile(CancellationToken cancellationToken) =>
        Ok(await jobAppService.GetMyWorkerProfileAsync(cancellationToken));

    /// <summary>Buyers and sellers only. Also updates their phone number and NIN when sent.</summary>
    [HttpPut("worker-profile")]
    [Authorize(Roles = "Buyer,Seller")]
    public async Task<ActionResult<WorkerProfileDto>> SaveMyWorkerProfile(SaveWorkerProfileRequest request, CancellationToken cancellationToken) =>
        Ok(await jobAppService.SaveMyWorkerProfileAsync(request, cancellationToken));
}
