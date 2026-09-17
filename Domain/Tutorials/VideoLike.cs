using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Tutorials;

public class VideoLike : Entity
{
    public Guid VideoId { get; private init; }
    public Guid UserId { get; private init; }
    public DateTimeOffset LikedAt { get; private init; }

    private VideoLike(Guid videoId, Guid userId)
    {
        VideoId = videoId;
        UserId = userId;
        LikedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private VideoLike()
    {
    }

    public static VideoLike Create(Guid videoId, Guid userId) => new(videoId, userId);
}
