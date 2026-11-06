using System.Net.Mail;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Users;
using AgrismartAPI.Domain.Jobs;
using AgrismartAPI.Domain.Logistics;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Jobs;

public record CreateJobRequest(
    string? Title,
    string? Description,
    string? District,
    int? WorkersNeeded,
    IReadOnlyList<string>? Skills,
    DateOnly? StartDate,
    string? Pay,
    string? ContactPhone,
    string? ContactEmail);

public record JobDto(
    Guid Id,
    Guid PostedByUserId,
    string Title,
    string Description,
    string District,
    int WorkersNeeded,
    IReadOnlyList<string> Skills,
    DateOnly? StartDate,
    string? Pay,
    string? ContactPhone,
    string? ContactEmail,
    JobStatus Status,
    DateTimeOffset CreatedAt);

/// <summary>
/// PhoneNumber and Nin update the worker's account, not a copy on the profile. Leave either out to keep the current value.
/// </summary>
public record SaveWorkerProfileRequest(
    string? Headline,
    string? Bio,
    string? District,
    IReadOnlyList<string>? Skills,
    bool IsAvailable = true,
    string? PhoneNumber = null,
    string? Nin = null);

/// <summary>PhoneNumber, Email and Nin come from the account. Email is read-only here because it is the login.</summary>
public record WorkerProfileDto(string Headline, string Bio, string District, IReadOnlyList<string> Skills, bool IsAvailable, DateTimeOffset UpdatedAt, string? PhoneNumber, string Email, string? Nin);

public record ApplyToJobRequest(string? Message);

/// <summary>Only admins see these, because they include the applicant's phone number.</summary>
public record JobApplicantDto(Guid ApplicationId, Guid WorkerUserId, string Name, string? PhoneNumber, string? Email, string? Headline, string? District, IReadOnlyList<string> Skills, string? Message, DateTimeOffset AppliedAt);

public class JobAppService(IJobRepository jobRepository, IUserRepository userRepository, ICurrentUserService currentUser, UserAppService userAppService)
{
    private const int MaxSkills = 20;
    private const int MaxSkillLength = 50;

    public async Task<JobDto> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default)
    {
        var title = Required(request.Title, "title", JobPost.MaxTitleLength);
        var description = Required(request.Description, "description", JobPost.MaxDescriptionLength);
        var district = RequireDistrict(request.District);
        MaxLength(request.Pay, "pay", JobPost.MaxPayLength);
        MaxLength(request.ContactPhone, "contactPhone", JobPost.MaxContactPhoneLength);
        ValidateEmail(request.ContactEmail, "contactEmail", JobPost.MaxContactEmailLength);

        if (request.WorkersNeeded is not (>= 1 and <= 1000))
        {
            throw Invalid("workersNeeded", "workersNeeded must be between 1 and 1000.");
        }

        var job = JobPost.Create(
            currentUser.UserId, title, description, district, request.WorkersNeeded.Value,
            CleanSkills(request.Skills), request.StartDate, request.Pay, request.ContactPhone, request.ContactEmail);

        await jobRepository.AddJobAsync(job, cancellationToken);
        return ToDto(job);
    }

    public async Task<IReadOnlyList<JobDto>> GetOpenAsync(string? district, string? skill, CancellationToken cancellationToken = default)
    {
        var canonicalDistrict = string.IsNullOrWhiteSpace(district) ? null : RequireDistrict(district);
        var jobs = await jobRepository.GetOpenJobsAsync(canonicalDistrict, cancellationToken);

        return jobs
            .Where(job => string.IsNullOrWhiteSpace(skill)
                          || job.Skills.Any(jobSkill => jobSkill.Contains(skill.Trim(), StringComparison.OrdinalIgnoreCase)))
            .Select(ToDto)
            .ToList();
    }

    public async Task<JobDto> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        ToDto(await FindJobAsync(id, cancellationToken));

    public async Task<JobDto> CloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await FindManagedJobAsync(id, cancellationToken);
        job.Close();
        await jobRepository.UpdateJobAsync(job, cancellationToken);
        return ToDto(job);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var job = await FindManagedJobAsync(id, cancellationToken);
        await jobRepository.DeleteJobAsync(job, cancellationToken);
    }

    public async Task<WorkerProfileDto> SaveMyWorkerProfileAsync(SaveWorkerProfileRequest request, CancellationToken cancellationToken = default)
    {
        var headline = Required(request.Headline, "headline", WorkerProfile.MaxHeadlineLength);
        var bio = Required(request.Bio, "bio", WorkerProfile.MaxBioLength);
        var district = RequireDistrict(request.District);
        var skills = CleanSkills(request.Skills);

        // After the profile checks, so a rejected profile never changes the account.
        var account = await userAppService.UpdateMyAccountAsync(
            new UpdateMyAccountRequest(request.PhoneNumber, request.Nin), cancellationToken);

        var profile = await jobRepository.GetWorkerProfileAsync(currentUser.UserId, cancellationToken);
        if (profile is null)
        {
            profile = WorkerProfile.Create(currentUser.UserId, headline, bio, district, skills, request.IsAvailable);
            await jobRepository.AddWorkerProfileAsync(profile, cancellationToken);
        }
        else
        {
            profile.Update(headline, bio, district, skills, request.IsAvailable);
            await jobRepository.UpdateWorkerProfileAsync(profile, cancellationToken);
        }

        return ToDto(profile, account.PhoneNumber, account.Email, account.Nin);
    }

    public async Task<WorkerProfileDto> GetMyWorkerProfileAsync(CancellationToken cancellationToken = default)
    {
        var profile = await jobRepository.GetWorkerProfileAsync(currentUser.UserId, cancellationToken)
                      ?? throw new NotFoundException("You have not created a worker profile yet.");

        var user = await userRepository.GetByIdAsync(currentUser.UserId, cancellationToken)
                   ?? throw new NotFoundException("Your account was not found.");

        return ToDto(profile, user.PhoneNumber, user.Email, user.Nin);
    }

    public async Task ApplyAsync(Guid jobId, ApplyToJobRequest request, CancellationToken cancellationToken = default)
    {
        MaxLength(request.Message, "message", JobApplication.MaxMessageLength);

        var job = await FindJobAsync(jobId, cancellationToken);
        if (job.Status != JobStatus.Open)
        {
            throw new ConflictException("This job is closed.");
        }

        if (job.PostedByUserId == currentUser.UserId)
        {
            throw new ValidationException("You cannot apply to your own job.");
        }

        if (await jobRepository.GetWorkerProfileAsync(currentUser.UserId, cancellationToken) is null)
        {
            throw new ValidationException("Create a worker profile before applying.");
        }

        if (await jobRepository.HasAppliedAsync(jobId, currentUser.UserId, cancellationToken))
        {
            throw new ConflictException("You have already applied to this job.");
        }

        await jobRepository.AddApplicationAsync(JobApplication.Create(jobId, currentUser.UserId, request.Message), cancellationToken);
    }

    public async Task<IReadOnlyList<JobApplicantDto>> GetApplicantsAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        await FindManagedJobAsync(jobId, cancellationToken);

        var applications = await jobRepository.GetApplicationsAsync(jobId, cancellationToken);
        var users = (await userRepository.GetByIdsAsync(applications.Select(application => application.WorkerUserId), cancellationToken))
            .ToDictionary(user => user.Id);

        var applicants = new List<JobApplicantDto>();
        foreach (var application in applications)
        {
            var user = users.GetValueOrDefault(application.WorkerUserId);
            var profile = await jobRepository.GetWorkerProfileAsync(application.WorkerUserId, cancellationToken);
            applicants.Add(new JobApplicantDto(
                application.Id, application.WorkerUserId, user?.Name ?? "Unknown", user?.PhoneNumber, user?.Email,
                profile?.Headline, profile?.District, profile?.Skills ?? [], application.Message, application.CreatedAt));
        }

        return applicants;
    }

    /// <summary>The job, if the caller posted it or is an admin. Anyone else gets a 403.</summary>
    public async Task<JobPost> FindManagedJobAsync(Guid id, CancellationToken cancellationToken)
    {
        var job = await FindJobAsync(id, cancellationToken);
        if (job.PostedByUserId != currentUser.UserId && currentUser.Role != UserRole.Admin)
        {
            throw new ForbiddenException("Only an admin can do that.");
        }

        return job;
    }

    private async Task<JobPost> FindJobAsync(Guid id, CancellationToken cancellationToken) =>
        await jobRepository.GetJobAsync(id, cancellationToken)
        ?? throw new NotFoundException($"Job '{id}' was not found.");

    private static IReadOnlyList<string> CleanSkills(IReadOnlyList<string>? skills)
    {
        var cleaned = (skills ?? [])
            .Where(skill => !string.IsNullOrWhiteSpace(skill))
            .Select(skill => skill.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cleaned.Count > MaxSkills || cleaned.Any(skill => skill.Length > MaxSkillLength))
        {
            throw Invalid("skills", $"List at most {MaxSkills} skills, each at most {MaxSkillLength} characters.");
        }

        return cleaned;
    }

    private static string RequireDistrict(string? value) =>
        SierraLeoneDistricts.Find(value)?.Name ?? throw Invalid("district", "district must be a Sierra Leone district.");

    private static string Required(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(field, $"{field} is required.");
        }

        MaxLength(value, field, maxLength);
        return value;
    }

    private static void MaxLength(string? value, string field, int maxLength)
    {
        if (value?.Trim().Length > maxLength)
        {
            throw Invalid(field, $"{field} must be at most {maxLength} characters.");
        }
    }

    private static void ValidateEmail(string? value, string field, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        MaxLength(value, field, maxLength);

        var email = value.Trim();
        if (!MailAddress.TryCreate(email, out var address) || address.Address != email)
        {
            throw Invalid(field, $"{field} must be a valid email address.");
        }
    }

    private static ValidationException Invalid(string field, string error) =>
        new(error, new Dictionary<string, string[]> { [field] = [error] });

    private static JobDto ToDto(JobPost job) => new(
        job.Id, job.PostedByUserId, job.Title, job.Description, job.District, job.WorkersNeeded,
        job.Skills, job.StartDate, job.Pay, job.ContactPhone, job.ContactEmail, job.Status, job.CreatedAt);

    private static WorkerProfileDto ToDto(WorkerProfile profile, string? phoneNumber, string email, string? nin) => new(
        profile.Headline, profile.Bio, profile.District, profile.Skills, profile.IsAvailable, profile.UpdatedAt, phoneNumber, email, nin);
}
