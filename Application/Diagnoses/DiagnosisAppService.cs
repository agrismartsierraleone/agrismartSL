using AgrismartAPI.Application.Abstractions;

namespace AgrismartAPI.Application.Diagnoses;

public class DiagnosisAppService(IDiagnosisEngine diagnosisEngine)
{
    public async Task<DiagnosisResultDto> DiagnoseAsync(
        Stream imageStream,
        string fileName,
        double? latitude = null,
        double? longitude = null,
        CancellationToken cancellationToken = default)
    {
        var result = await diagnosisEngine.DiagnoseAsync(imageStream, fileName, latitude, longitude, cancellationToken);
        return DiagnosisMapper.ToDto(result);
    }
}
