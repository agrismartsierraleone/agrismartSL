namespace AgrismartAPI.Domain.Harvest;

public interface ICropRepository
{
    Task<IReadOnlyList<CropProfile>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CropProfile?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(CropProfile crop, CancellationToken cancellationToken = default);
}
