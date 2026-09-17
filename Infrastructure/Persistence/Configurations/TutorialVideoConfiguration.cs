using AgrismartAPI.Domain.Tutorials;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgrismartAPI.Infrastructure.Persistence.Configurations;

public class TutorialVideoConfiguration : IEntityTypeConfiguration<TutorialVideo>
{
    public void Configure(EntityTypeBuilder<TutorialVideo> builder)
    {
        builder.ToTable("TutorialVideos");
        builder.HasKey(video => video.Id);

        builder.Property(video => video.Title).IsRequired().HasMaxLength(200);
        builder.Property(video => video.Description).HasMaxLength(2000);
        builder.Property(video => video.VideoUrl).IsRequired().HasMaxLength(2048);
        builder.Property(video => video.ThumbnailUrl).IsRequired().HasMaxLength(2048);
        builder.Property(video => video.Instructor).IsRequired().HasMaxLength(200);
        builder.Property(video => video.InstructorAvatarEmoji).HasMaxLength(16);
        builder.Property(video => video.DurationLabel).HasMaxLength(20);
    }
}

public class VideoCommentConfiguration : IEntityTypeConfiguration<VideoComment>
{
    public void Configure(EntityTypeBuilder<VideoComment> builder)
    {
        builder.ToTable("VideoComments");
        builder.HasKey(comment => comment.Id);

        builder.Property(comment => comment.AuthorName).IsRequired().HasMaxLength(200);
        builder.Property(comment => comment.Text).IsRequired().HasMaxLength(2000);
        builder.Property(comment => comment.CreatedAt).IsRequired();

        builder.HasIndex(comment => comment.VideoId);
    }
}

public class VideoLikeConfiguration : IEntityTypeConfiguration<VideoLike>
{
    public void Configure(EntityTypeBuilder<VideoLike> builder)
    {
        builder.ToTable("VideoLikes");
        builder.HasKey(like => like.Id);

        builder.HasIndex(like => new { like.VideoId, like.UserId }).IsUnique();
    }
}
