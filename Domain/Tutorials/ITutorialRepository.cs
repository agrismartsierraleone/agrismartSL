namespace AgrismartAPI.Domain.Tutorials;

public interface ITutorialRepository
{
    Task<IReadOnlyList<TutorialVideo>> GetAllVideosAsync(CancellationToken cancellationToken = default);

    Task<TutorialVideo?> GetVideoByIdAsync(Guid videoId, CancellationToken cancellationToken = default);

    Task AddAsync(TutorialVideo video, CancellationToken cancellationToken = default);

    Task UpdateAsync(TutorialVideo video, CancellationToken cancellationToken = default);

    Task DeleteAsync(TutorialVideo video, CancellationToken cancellationToken = default);

    Task<HashSet<Guid>> GetLikedVideoIdsAsync(Guid userId, IEnumerable<Guid> videoIds, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VideoComment>> GetCommentsAsync(Guid videoId, CancellationToken cancellationToken = default);

    Task<VideoComment> AddCommentAsync(VideoComment comment, CancellationToken cancellationToken = default);

    /// <summary>Sets the like state for (videoId, userId) and returns the video's updated LikeCount.</summary>
    Task<int> SetLikeAsync(Guid videoId, Guid userId, bool liked, CancellationToken cancellationToken = default);
}
