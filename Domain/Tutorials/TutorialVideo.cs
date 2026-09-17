using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Tutorials;

public class TutorialVideo : Entity
{
    public string Title { get; private set; }
    public string Description { get; private set; }
    public string VideoUrl { get; private set; }
    public string ThumbnailUrl { get; private set; }
    public string Instructor { get; private set; }
    public string InstructorAvatarEmoji { get; private set; }
    public string DurationLabel { get; private set; }
    public int Views { get; private set; }
    public DateTimeOffset UploadedAt { get; private set; }
    public int LikeCount { get; private set; }
    public int CommentCount { get; private set; }

    private TutorialVideo(
        string title,
        string description,
        string videoUrl,
        string thumbnailUrl,
        string instructor,
        string instructorAvatarEmoji,
        string durationLabel,
        int views,
        DateTimeOffset uploadedAt)
    {
        Title = title;
        Description = description;
        VideoUrl = videoUrl;
        ThumbnailUrl = thumbnailUrl;
        Instructor = instructor;
        InstructorAvatarEmoji = instructorAvatarEmoji;
        DurationLabel = durationLabel;
        Views = views;
        UploadedAt = uploadedAt;
        LikeCount = 0;
        CommentCount = 0;
    }

    // EF Core
    private TutorialVideo()
    {
        Title = null!;
        Description = null!;
        VideoUrl = null!;
        ThumbnailUrl = null!;
        Instructor = null!;
        InstructorAvatarEmoji = null!;
        DurationLabel = null!;
    }

    public static TutorialVideo Create(
        string title,
        string description,
        string videoUrl,
        string thumbnailUrl,
        string instructor,
        string instructorAvatarEmoji,
        string durationLabel,
        int views,
        DateTimeOffset uploadedAt)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        return new TutorialVideo(
            title.Trim(),
            description,
            videoUrl,
            thumbnailUrl,
            instructor,
            instructorAvatarEmoji,
            durationLabel,
            views,
            uploadedAt);
    }

    public void Update(
        string title,
        string description,
        string videoUrl,
        string thumbnailUrl,
        string instructor,
        string instructorAvatarEmoji,
        string durationLabel)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        Title = title.Trim();
        Description = description;
        VideoUrl = videoUrl;
        ThumbnailUrl = thumbnailUrl;
        Instructor = instructor;
        InstructorAvatarEmoji = instructorAvatarEmoji;
        DurationLabel = durationLabel;
    }

    public void SetLikeCount(int likeCount) => LikeCount = Math.Max(0, likeCount);

    public void IncrementCommentCount() => CommentCount += 1;
}
