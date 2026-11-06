using AgrismartAPI.Domain.Jobs;
using AgrismartAPI.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class JobPostConfiguration : IEntityTypeConfiguration<JobPost>
{
    public void Configure(EntityTypeBuilder<JobPost> builder)
    {
        builder.ToTable("JobPosts");
        builder.HasKey(job => job.Id);

        builder.Property(job => job.Title).IsRequired().HasMaxLength(JobPost.MaxTitleLength);
        builder.Property(job => job.Description).IsRequired().HasMaxLength(JobPost.MaxDescriptionLength);
        builder.Property(job => job.District).IsRequired().HasMaxLength(50);
        builder.Property(job => job.Pay).HasMaxLength(JobPost.MaxPayLength);
        builder.Property(job => job.ContactPhone).HasMaxLength(JobPost.MaxContactPhoneLength);
        builder.Property(job => job.ContactEmail).HasMaxLength(JobPost.MaxContactEmailLength);
        builder.Property(job => job.Status).HasConversion<string>().HasMaxLength(20);

        var skills = builder.Property(job => job.Skills).HasConversion(JsonValueConverters.ForList<string>());
        skills.Metadata.SetValueComparer(JsonValueConverters.ListComparer<string>());

        builder.HasOne<User>().WithMany().HasForeignKey(job => job.PostedByUserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(job => new { job.Status, job.District });
        builder.HasIndex(job => job.PostedByUserId);
    }
}

public class WorkerProfileConfiguration : IEntityTypeConfiguration<WorkerProfile>
{
    public void Configure(EntityTypeBuilder<WorkerProfile> builder)
    {
        builder.ToTable("WorkerProfiles");
        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Headline).IsRequired().HasMaxLength(WorkerProfile.MaxHeadlineLength);
        builder.Property(profile => profile.Bio).IsRequired().HasMaxLength(WorkerProfile.MaxBioLength);
        builder.Property(profile => profile.District).IsRequired().HasMaxLength(50);

        var skills = builder.Property(profile => profile.Skills).HasConversion(JsonValueConverters.ForList<string>());
        skills.Metadata.SetValueComparer(JsonValueConverters.ListComparer<string>());

        builder.HasOne<User>().WithMany().HasForeignKey(profile => profile.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(profile => profile.UserId).IsUnique();
    }
}

public class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("JobApplications");
        builder.HasKey(application => application.Id);

        builder.Property(application => application.Message).HasMaxLength(JobApplication.MaxMessageLength);

        builder.HasOne<JobPost>().WithMany().HasForeignKey(application => application.JobPostId).OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<User>().WithMany().HasForeignKey(application => application.WorkerUserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(application => new { application.JobPostId, application.WorkerUserId }).IsUnique();
        builder.HasIndex(application => application.WorkerUserId);
    }
}
