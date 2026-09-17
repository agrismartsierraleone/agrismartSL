namespace AgrismartAPI.Controllers.Requests;

public class TutorialVideoForm
{
    public required string Title { get; set; }
    public required string Description { get; set; }

    /// <summary>Optional when a VideoFile is uploaded instead.</summary>
    public string? VideoUrl { get; set; }

    public IFormFile? VideoFile { get; set; }

    public required string ThumbnailUrl { get; set; }
    public required string Instructor { get; set; }
    public required string InstructorAvatarEmoji { get; set; }
    public required string DurationLabel { get; set; }
}
