using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Tutorials;
using AgrismartAPI.Controllers.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgrismartAPI.Controllers;

[ApiController]
[Route("tutorials")]
[Authorize]
public class TutorialsController(TutorialAppService tutorialAppService, ICurrentUserService currentUser) : ControllerBase
{
    [HttpGet("videos")]
    public async Task<ActionResult<IReadOnlyList<TutorialVideoDto>>> GetVideos(CancellationToken cancellationToken)
    {
        var videos = await tutorialAppService.GetVideosAsync(currentUser.UserId, cancellationToken);
        return Ok(videos.Select(WithAbsoluteUrls));
    }

    [HttpPost("videos")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<TutorialVideoDto>> CreateVideo([FromForm] TutorialVideoForm form, CancellationToken cancellationToken)
    {
        var request = new CreateTutorialVideoRequest(
            form.Title,
            form.Description,
            form.VideoUrl,
            ToUploadedFile(form.VideoFile),
            form.ThumbnailUrl,
            form.Instructor,
            form.InstructorAvatarEmoji,
            form.DurationLabel);

        var video = await tutorialAppService.CreateVideoAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetVideos), new { }, WithAbsoluteUrls(video));
    }

    [HttpPut("videos/{videoId:guid}")]
    [Authorize(Roles = "Admin")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(200 * 1024 * 1024)]
    public async Task<ActionResult<TutorialVideoDto>> UpdateVideo(Guid videoId, [FromForm] TutorialVideoForm form, CancellationToken cancellationToken)
    {
        var request = new UpdateTutorialVideoRequest(
            form.Title,
            form.Description,
            form.VideoUrl,
            ToUploadedFile(form.VideoFile),
            form.ThumbnailUrl,
            form.Instructor,
            form.InstructorAvatarEmoji,
            form.DurationLabel);

        var video = await tutorialAppService.UpdateVideoAsync(currentUser.UserId, videoId, request, cancellationToken);
        return Ok(WithAbsoluteUrls(video));
    }

    [HttpDelete("videos/{videoId:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteVideo(Guid videoId, CancellationToken cancellationToken)
    {
        await tutorialAppService.DeleteVideoAsync(videoId, cancellationToken);
        return NoContent();
    }

    [HttpGet("videos/{videoId:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<VideoCommentDto>>> GetComments(Guid videoId, CancellationToken cancellationToken)
    {
        var comments = await tutorialAppService.GetCommentsAsync(videoId, cancellationToken);
        return Ok(comments);
    }

    [HttpPost("videos/{videoId:guid}/like")]
    public async Task<ActionResult<LikeResponse>> SetLike(Guid videoId, LikeRequest request, CancellationToken cancellationToken)
    {
        var response = await tutorialAppService.SetLikeAsync(videoId, currentUser.UserId, request.Liked, cancellationToken);
        return Ok(response);
    }

    [HttpPost("videos/{videoId:guid}/comments")]
    public async Task<ActionResult<VideoCommentDto>> AddComment(Guid videoId, CreateCommentRequest request, CancellationToken cancellationToken)
    {
        var comment = await tutorialAppService.AddCommentAsync(videoId, currentUser.UserId, request.Text, cancellationToken);
        return Ok(comment);
    }

    private static UploadedFile? ToUploadedFile(IFormFile? file)
    {
        return file is null ? null : new UploadedFile(file.OpenReadStream(), file.FileName, file.ContentType);
    }

    private TutorialVideoDto WithAbsoluteUrls(TutorialVideoDto video)
    {
        return video with
        {
            VideoUrl = UploadUrlHelper.ToAbsolute(Request, video.VideoUrl)!,
            ThumbnailUrl = UploadUrlHelper.ToAbsolute(Request, video.ThumbnailUrl)!
        };
    }
}
