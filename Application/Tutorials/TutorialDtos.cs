using AgrismartAPI.Application.Common;

namespace AgrismartAPI.Application.Tutorials;

public record TutorialVideoDto(
    Guid Id,
    string Title,
    string Description,
    string VideoUrl,
    string ThumbnailUrl,
    string Instructor,
    string InstructorAvatarEmoji,
    string DurationLabel,
    int Views,
    DateTimeOffset UploadedAt,
    int LikeCount,
    int CommentCount,
    bool IsLiked);

public record VideoCommentDto(
    Guid Id,
    Guid VideoId,
    string AuthorName,
    string Text,
    DateTimeOffset CreatedAt);

public record CreateTutorialVideoRequest(
    string Title,
    string Description,
    string? VideoUrl,
    UploadedFile? VideoFile,
    string ThumbnailUrl,
    string Instructor,
    string InstructorAvatarEmoji,
    string DurationLabel);

public record UpdateTutorialVideoRequest(
    string Title,
    string Description,
    string? VideoUrl,
    UploadedFile? VideoFile,
    string ThumbnailUrl,
    string Instructor,
    string InstructorAvatarEmoji,
    string DurationLabel);

public record LikeRequest(bool Liked);

public record LikeResponse(int LikeCount);

public record CreateCommentRequest(string Text);
