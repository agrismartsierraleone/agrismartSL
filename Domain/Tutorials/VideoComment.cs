using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Tutorials;

public class VideoComment : Entity
{
    public Guid VideoId { get; private init; }
    public Guid UserId { get; private init; }
    public string AuthorName { get; private init; }
    public string Text { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private VideoComment(Guid videoId, Guid userId, string authorName, string text)
    {
        VideoId = videoId;
        UserId = userId;
        AuthorName = authorName;
        Text = text;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private VideoComment()
    {
        AuthorName = null!;
        Text = null!;
    }

    public static VideoComment Create(Guid videoId, Guid userId, string authorName, string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Comment text is required.", nameof(text));
        }

        return new VideoComment(videoId, userId, authorName, text.Trim());
    }
}
