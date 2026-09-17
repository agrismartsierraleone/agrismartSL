namespace AgrismartAPI.Controllers.Requests;

/// <summary>Resolves a stored upload path (e.g. "/uploads/products/x.jpg") into an absolute URL for the current request. Leaves already-absolute URLs (external links) untouched.</summary>
public static class UploadUrlHelper
{
    public static string? ToAbsolute(HttpRequest request, string? relativeOrAbsoluteUrl)
    {
        if (string.IsNullOrEmpty(relativeOrAbsoluteUrl) || !relativeOrAbsoluteUrl.StartsWith('/'))
        {
            return relativeOrAbsoluteUrl;
        }

        return $"{request.Scheme}://{request.Host}{relativeOrAbsoluteUrl}";
    }
}
