using AgrismartAPI.Application.Common;

namespace AgrismartAPI.Application.Abstractions;

public interface IFileStorageService
{
    /// <summary>Saves the file under the given subfolder and returns a root-relative URL (e.g. "/uploads/products/{name}").</summary>
    Task<string> SaveAsync(UploadedFile file, string subfolder, CancellationToken cancellationToken = default);

    /// <summary>Deletes a previously saved file. Safe to call with an external URL or null — those are ignored.</summary>
    void DeleteIfLocal(string? relativeUrl);
}
