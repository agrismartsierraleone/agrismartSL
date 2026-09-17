using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Diagnoses;
using AgrismartAPI.Domain.Scans;

namespace AgrismartAPI.Application.Scans;

public class ScanAppService(
    IScanHistoryRepository scanHistoryRepository,
    IDiagnosisEngine diagnosisEngine,
    IFileStorageService fileStorageService)
{
    public async Task<IReadOnlyList<PlantScanDto>> GetHistoryAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var scans = await scanHistoryRepository.GetAllForUserAsync(userId, cancellationToken);
        return scans
            .OrderByDescending(scan => scan.ScannedAt)
            .Select(ToDto)
            .ToList();
    }

    /// <summary>
    /// Diagnoses the uploaded photo (via Kindwise, or the mock engine in local dev) and saves both the
    /// image and the resulting diagnosis to the user's scan history in one call.
    /// </summary>
    public async Task<PlantScanDto> CreateAsync(
        Guid userId,
        Stream imageStream,
        string fileName,
        string contentType,
        double? latitude = null,
        double? longitude = null,
        CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await imageStream.CopyToAsync(buffer, cancellationToken);

        buffer.Position = 0;
        var diagnosis = await diagnosisEngine.DiagnoseAsync(buffer, fileName, latitude, longitude, cancellationToken);

        buffer.Position = 0;
        var imageUrl = await fileStorageService.SaveAsync(new UploadedFile(buffer, fileName, contentType), "scans", cancellationToken);

        var scan = PlantScan.Create(userId, imageUrl, diagnosis);
        await scanHistoryRepository.AddAsync(scan, cancellationToken);
        return ToDto(scan);
    }

    private static PlantScanDto ToDto(PlantScan scan) => new(
        scan.Id,
        scan.ImagePath,
        DiagnosisMapper.ToDto(scan.Diagnosis),
        scan.ScannedAt);
}
