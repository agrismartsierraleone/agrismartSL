using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Tutorials;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class TutorialRepository(AppDbContext dbContext) : ITutorialRepository
{
    public async Task<IReadOnlyList<TutorialVideo>> GetAllVideosAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.TutorialVideos
            .OrderByDescending(video => video.UploadedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<TutorialVideo?> GetVideoByIdAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        return dbContext.TutorialVideos.SingleOrDefaultAsync(video => video.Id == videoId, cancellationToken);
    }

    public async Task AddAsync(TutorialVideo video, CancellationToken cancellationToken = default)
    {
        dbContext.TutorialVideos.Add(video);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TutorialVideo video, CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TutorialVideo video, CancellationToken cancellationToken = default)
    {
        var comments = await dbContext.VideoComments.Where(comment => comment.VideoId == video.Id).ToListAsync(cancellationToken);
        var likes = await dbContext.VideoLikes.Where(like => like.VideoId == video.Id).ToListAsync(cancellationToken);

        dbContext.VideoComments.RemoveRange(comments);
        dbContext.VideoLikes.RemoveRange(likes);
        dbContext.TutorialVideos.Remove(video);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<HashSet<Guid>> GetLikedVideoIdsAsync(Guid userId, IEnumerable<Guid> videoIds, CancellationToken cancellationToken = default)
    {
        var ids = videoIds.ToList();
        var liked = await dbContext.VideoLikes
            .Where(like => like.UserId == userId && ids.Contains(like.VideoId))
            .Select(like => like.VideoId)
            .ToListAsync(cancellationToken);

        return liked.ToHashSet();
    }

    public async Task<IReadOnlyList<VideoComment>> GetCommentsAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        return await dbContext.VideoComments
            .Where(comment => comment.VideoId == videoId)
            .OrderBy(comment => comment.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<VideoComment> AddCommentAsync(VideoComment comment, CancellationToken cancellationToken = default)
    {
        var video = await dbContext.TutorialVideos.SingleOrDefaultAsync(v => v.Id == comment.VideoId, cancellationToken)
                    ?? throw new NotFoundException($"Tutorial video '{comment.VideoId}' was not found.");

        video.IncrementCommentCount();
        dbContext.VideoComments.Add(comment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return comment;
    }

    public async Task<int> SetLikeAsync(Guid videoId, Guid userId, bool liked, CancellationToken cancellationToken = default)
    {
        var video = await dbContext.TutorialVideos.SingleOrDefaultAsync(v => v.Id == videoId, cancellationToken)
                    ?? throw new NotFoundException($"Tutorial video '{videoId}' was not found.");

        var existingLike = await dbContext.VideoLikes
            .SingleOrDefaultAsync(like => like.VideoId == videoId && like.UserId == userId, cancellationToken);

        var currentCount = await dbContext.VideoLikes.CountAsync(like => like.VideoId == videoId, cancellationToken);

        int newCount;
        if (liked && existingLike is null)
        {
            dbContext.VideoLikes.Add(VideoLike.Create(videoId, userId));
            newCount = currentCount + 1;
        }
        else if (!liked && existingLike is not null)
        {
            dbContext.VideoLikes.Remove(existingLike);
            newCount = currentCount - 1;
        }
        else
        {
            newCount = currentCount;
        }

        video.SetLikeCount(newCount);
        await dbContext.SaveChangesAsync(cancellationToken);
        return newCount;
    }
}
