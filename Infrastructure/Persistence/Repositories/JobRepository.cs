using AgrismartAPI.Domain.Jobs;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class JobRepository(AppDbContext dbContext) : IJobRepository
{
    public async Task<IReadOnlyList<JobPost>> GetOpenJobsAsync(string? district, CancellationToken cancellationToken = default)
    {
        var query = dbContext.JobPosts.Where(job => job.Status == JobStatus.Open);
        if (district is not null)
        {
            query = query.Where(job => job.District == district);
        }

        return await query.OrderByDescending(job => job.CreatedAt).ToListAsync(cancellationToken);
    }

    public Task<JobPost?> GetJobAsync(Guid id, CancellationToken cancellationToken = default) =>
        dbContext.JobPosts.SingleOrDefaultAsync(job => job.Id == id, cancellationToken);

    public async Task AddJobAsync(JobPost job, CancellationToken cancellationToken = default)
    {
        dbContext.JobPosts.Add(job);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateJobAsync(JobPost job, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteJobAsync(JobPost job, CancellationToken cancellationToken = default)
    {
        dbContext.JobPosts.Remove(job);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<WorkerProfile?> GetWorkerProfileAsync(Guid userId, CancellationToken cancellationToken = default) =>
        dbContext.WorkerProfiles.SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);

    public async Task<IReadOnlyList<WorkerProfile>> GetAvailableWorkersAsync(CancellationToken cancellationToken = default) =>
        await dbContext.WorkerProfiles.Where(profile => profile.IsAvailable).ToListAsync(cancellationToken);

    public async Task AddWorkerProfileAsync(WorkerProfile profile, CancellationToken cancellationToken = default)
    {
        dbContext.WorkerProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateWorkerProfileAsync(WorkerProfile profile, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(Guid jobPostId, CancellationToken cancellationToken = default) =>
        await dbContext.JobApplications
            .Where(application => application.JobPostId == jobPostId)
            .OrderBy(application => application.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> HasAppliedAsync(Guid jobPostId, Guid workerUserId, CancellationToken cancellationToken = default) =>
        dbContext.JobApplications.AnyAsync(
            application => application.JobPostId == jobPostId && application.WorkerUserId == workerUserId, cancellationToken);

    public async Task AddApplicationAsync(JobApplication application, CancellationToken cancellationToken = default)
    {
        dbContext.JobApplications.Add(application);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
