using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Jobs;

public enum JobStatus
{
    Open,
    Closed
}

public class JobPost : Entity
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxPayLength = 200;
    public const int MaxContactPhoneLength = 30;
    public const int MaxContactEmailLength = 254;

    public Guid PostedByUserId { get; private init; }
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string District { get; private set; }
    public int WorkersNeeded { get; private set; }
    public IReadOnlyList<string> Skills { get; private set; }
    public DateOnly? StartDate { get; private set; }

    /// <summary>What the poster offers, in their own words. Never filled in by the AI.</summary>
    public string? Pay { get; private set; }

    public string? ContactPhone { get; private set; }
    public string? ContactEmail { get; private set; }
    public JobStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    private JobPost(Guid postedByUserId, string title, string description, string district, int workersNeeded, IReadOnlyList<string> skills, DateOnly? startDate, string? pay, string? contactPhone, string? contactEmail)
    {
        PostedByUserId = postedByUserId;
        Title = title;
        Description = description;
        District = district;
        WorkersNeeded = workersNeeded;
        Skills = skills;
        StartDate = startDate;
        Pay = pay;
        ContactPhone = contactPhone;
        ContactEmail = contactEmail;
        Status = JobStatus.Open;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private JobPost()
    {
        Title = null!;
        Description = null!;
        District = null!;
        Skills = [];
    }

    public static JobPost Create(Guid postedByUserId, string title, string description, string district, int workersNeeded, IReadOnlyList<string> skills, DateOnly? startDate, string? pay, string? contactPhone, string? contactEmail) =>
        new(postedByUserId, title.Trim(), description.Trim(), district, workersNeeded, skills, startDate, pay?.Trim(), contactPhone?.Trim(), string.IsNullOrWhiteSpace(contactEmail) ? null : contactEmail.Trim());

    public void Close() => Status = JobStatus.Closed;
}

/// <summary>A worker's public profile. Workers appear in matches only by creating one.</summary>
public class WorkerProfile : Entity
{
    public const int MaxHeadlineLength = 200;
    public const int MaxBioLength = 1000;

    public Guid UserId { get; private init; }
    public string Headline { get; private set; }
    public string Bio { get; private set; }
    public string District { get; private set; }
    public IReadOnlyList<string> Skills { get; private set; }
    public bool IsAvailable { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private WorkerProfile(Guid userId, string headline, string bio, string district, IReadOnlyList<string> skills, bool isAvailable)
    {
        UserId = userId;
        Headline = headline;
        Bio = bio;
        District = district;
        Skills = skills;
        IsAvailable = isAvailable;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private WorkerProfile()
    {
        Headline = null!;
        Bio = null!;
        District = null!;
        Skills = [];
    }

    public static WorkerProfile Create(Guid userId, string headline, string bio, string district, IReadOnlyList<string> skills, bool isAvailable) =>
        new(userId, headline.Trim(), bio.Trim(), district, skills, isAvailable);

    public void Update(string headline, string bio, string district, IReadOnlyList<string> skills, bool isAvailable)
    {
        Headline = headline.Trim();
        Bio = bio.Trim();
        District = district;
        Skills = skills;
        IsAvailable = isAvailable;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}

public class JobApplication : Entity
{
    public const int MaxMessageLength = 500;

    public Guid JobPostId { get; private init; }
    public Guid WorkerUserId { get; private init; }
    public string? Message { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private JobApplication(Guid jobPostId, Guid workerUserId, string? message)
    {
        JobPostId = jobPostId;
        WorkerUserId = workerUserId;
        Message = message;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private JobApplication()
    {
    }

    public static JobApplication Create(Guid jobPostId, Guid workerUserId, string? message) =>
        new(jobPostId, workerUserId, string.IsNullOrWhiteSpace(message) ? null : message.Trim());
}

public interface IJobRepository
{
    Task<IReadOnlyList<JobPost>> GetOpenJobsAsync(string? district, CancellationToken cancellationToken = default);

    Task<JobPost?> GetJobAsync(Guid id, CancellationToken cancellationToken = default);

    Task AddJobAsync(JobPost job, CancellationToken cancellationToken = default);

    Task UpdateJobAsync(JobPost job, CancellationToken cancellationToken = default);

    Task DeleteJobAsync(JobPost job, CancellationToken cancellationToken = default);

    Task<WorkerProfile?> GetWorkerProfileAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkerProfile>> GetAvailableWorkersAsync(CancellationToken cancellationToken = default);

    Task AddWorkerProfileAsync(WorkerProfile profile, CancellationToken cancellationToken = default);

    Task UpdateWorkerProfileAsync(WorkerProfile profile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JobApplication>> GetApplicationsAsync(Guid jobPostId, CancellationToken cancellationToken = default);

    Task<bool> HasAppliedAsync(Guid jobPostId, Guid workerUserId, CancellationToken cancellationToken = default);

    Task AddApplicationAsync(JobApplication application, CancellationToken cancellationToken = default);
}
