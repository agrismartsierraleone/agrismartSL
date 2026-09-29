namespace AgrismartAPI.Domain.Fertilizer;

public interface IFertilizerCropRepository
{
    Task<IReadOnlyList<FertilizerCropProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FertilizerCropProfile?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(FertilizerCropProfile crop, CancellationToken cancellationToken = default);

    Task UpdateAsync(FertilizerCropProfile crop, CancellationToken cancellationToken = default);
}
