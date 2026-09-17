using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using Microsoft.AspNetCore.Hosting;

namespace AgrismartAPI.Infrastructure.Storage;

/// <summary>Saves uploads to wwwroot/uploads on local disk, served back out via static file middleware. Swap for a cloud-backed implementation (e.g. Azure Blob/S3) without touching callers.</summary>
public class LocalFileStorageService(IWebHostEnvironment environment) : IFileStorageService
{
    private const string UploadsUrlPrefix = "/uploads/";

    public async Task<string> SaveAsync(UploadedFile file, string subfolder, CancellationToken cancellationToken = default)
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var directory = Path.Combine(webRoot, "uploads", subfolder);
        Directory.CreateDirectory(directory);

        var extension = Path.GetExtension(file.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var physicalPath = Path.Combine(directory, fileName);

        await using (var destination = File.Create(physicalPath))
        {
            await file.Content.CopyToAsync(destination, cancellationToken);
        }

        return $"{UploadsUrlPrefix}{subfolder}/{fileName}";
    }

    public void DeleteIfLocal(string? relativeUrl)
    {
        if (string.IsNullOrWhiteSpace(relativeUrl) || !relativeUrl.StartsWith(UploadsUrlPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var relativePath = relativeUrl[1..].Replace('/', Path.DirectorySeparatorChar);
        var physicalPath = Path.Combine(webRoot, relativePath);

        if (File.Exists(physicalPath))
        {
            File.Delete(physicalPath);
        }
    }
}
