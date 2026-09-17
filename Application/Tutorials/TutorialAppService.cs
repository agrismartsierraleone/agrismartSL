using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Tutorials;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Application.Tutorials;

public class TutorialAppService(ITutorialRepository tutorialRepository, IUserRepository userRepository, IFileStorageService fileStorageService)
{
    private static readonly string[] AllowedVideoExtensions = [".mp4", ".mov", ".webm", ".mkv", ".avi"];

    public async Task<IReadOnlyList<TutorialVideoDto>> GetVideosAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var videos = await tutorialRepository.GetAllVideosAsync(cancellationToken);
        var likedVideoIds = await tutorialRepository.GetLikedVideoIdsAsync(userId, videos.Select(v => v.Id), cancellationToken);

        return videos
            .OrderByDescending(video => video.UploadedAt)
            .Select(video => ToDto(video, likedVideoIds.Contains(video.Id)))
            .ToList();
    }

    public async Task<IReadOnlyList<VideoCommentDto>> GetCommentsAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        await EnsureVideoExistsAsync(videoId, cancellationToken);

        var comments = await tutorialRepository.GetCommentsAsync(videoId, cancellationToken);
        return comments
            .OrderBy(comment => comment.CreatedAt)
            .Select(ToDto)
            .ToList();
    }

    public async Task<TutorialVideoDto> CreateVideoAsync(CreateTutorialVideoRequest request, CancellationToken cancellationToken = default)
    {
        var videoUrl = await ResolveVideoUrlAsync(request.VideoFile, request.VideoUrl, cancellationToken);

        var video = TutorialVideo.Create(
            request.Title,
            request.Description,
            videoUrl,
            request.ThumbnailUrl,
            request.Instructor,
            request.InstructorAvatarEmoji,
            request.DurationLabel,
            views: 0,
            uploadedAt: DateTimeOffset.UtcNow);

        await tutorialRepository.AddAsync(video, cancellationToken);
        return ToDto(video, isLiked: false);
    }

    public async Task<TutorialVideoDto> UpdateVideoAsync(Guid userId, Guid videoId, UpdateTutorialVideoRequest request, CancellationToken cancellationToken = default)
    {
        var video = await EnsureVideoExistsAsync(videoId, cancellationToken);

        var videoUrl = video.VideoUrl;
        if (request.VideoFile is not null)
        {
            videoUrl = await SaveVideoFileAsync(request.VideoFile, cancellationToken);
            fileStorageService.DeleteIfLocal(video.VideoUrl);
        }
        else if (!string.IsNullOrWhiteSpace(request.VideoUrl))
        {
            videoUrl = request.VideoUrl;
        }

        video.Update(
            request.Title,
            request.Description,
            videoUrl,
            request.ThumbnailUrl,
            request.Instructor,
            request.InstructorAvatarEmoji,
            request.DurationLabel);

        await tutorialRepository.UpdateAsync(video, cancellationToken);

        var likedIds = await tutorialRepository.GetLikedVideoIdsAsync(userId, [videoId], cancellationToken);
        return ToDto(video, likedIds.Contains(videoId));
    }

    public async Task DeleteVideoAsync(Guid videoId, CancellationToken cancellationToken = default)
    {
        var video = await EnsureVideoExistsAsync(videoId, cancellationToken);
        fileStorageService.DeleteIfLocal(video.VideoUrl);
        await tutorialRepository.DeleteAsync(video, cancellationToken);
    }

    public async Task<LikeResponse> SetLikeAsync(Guid videoId, Guid userId, bool liked, CancellationToken cancellationToken = default)
    {
        await EnsureVideoExistsAsync(videoId, cancellationToken);

        var likeCount = await tutorialRepository.SetLikeAsync(videoId, userId, liked, cancellationToken);
        return new LikeResponse(likeCount);
    }

    public async Task<VideoCommentDto> AddCommentAsync(Guid videoId, Guid userId, string text, CancellationToken cancellationToken = default)
    {
        await EnsureVideoExistsAsync(videoId, cancellationToken);

        var user = await userRepository.GetByIdAsync(userId, cancellationToken)
                   ?? throw new NotFoundException("User not found.");

        var comment = VideoComment.Create(videoId, userId, user.Name, text);
        var created = await tutorialRepository.AddCommentAsync(comment, cancellationToken);
        return ToDto(created);
    }

    private async Task<string> ResolveVideoUrlAsync(UploadedFile? videoFile, string? videoUrl, CancellationToken cancellationToken)
    {
        if (videoFile is not null)
        {
            return await SaveVideoFileAsync(videoFile, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(videoUrl))
        {
            throw new ValidationException("Either a video file upload or a videoUrl must be provided.");
        }

        return videoUrl;
    }

    private async Task<string> SaveVideoFileAsync(UploadedFile videoFile, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(videoFile.FileName).ToLowerInvariant();
        if (!AllowedVideoExtensions.Contains(extension))
        {
            throw new ValidationException("Video file must be one of: .mp4, .mov, .webm, .mkv, .avi.");
        }

        return await fileStorageService.SaveAsync(videoFile, "tutorials", cancellationToken);
    }

    private async Task<TutorialVideo> EnsureVideoExistsAsync(Guid videoId, CancellationToken cancellationToken)
    {
        return await tutorialRepository.GetVideoByIdAsync(videoId, cancellationToken)
               ?? throw new NotFoundException($"Tutorial video '{videoId}' was not found.");
    }

    private static TutorialVideoDto ToDto(TutorialVideo video, bool isLiked) => new(
        video.Id,
        video.Title,
        video.Description,
        video.VideoUrl,
        video.ThumbnailUrl,
        video.Instructor,
        video.InstructorAvatarEmoji,
        video.DurationLabel,
        video.Views,
        video.UploadedAt,
        video.LikeCount,
        video.CommentCount,
        isLiked);

    private static VideoCommentDto ToDto(VideoComment comment) => new(
        comment.Id,
        comment.VideoId,
        comment.AuthorName,
        comment.Text,
        comment.CreatedAt);
}
