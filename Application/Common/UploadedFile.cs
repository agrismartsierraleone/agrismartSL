namespace AgrismartAPI.Application.Common;

/// <summary>Framework-agnostic wrapper around an incoming file upload, so the Application layer isn't coupled to ASP.NET Core's IFormFile.</summary>
public record UploadedFile(Stream Content, string FileName, string ContentType);
