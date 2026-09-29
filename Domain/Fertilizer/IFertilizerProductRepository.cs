namespace AgrismartAPI.Domain.Fertilizer;

public interface IFertilizerProductRepository
{
    Task<IReadOnlyList<FertilizerProduct>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<FertilizerProduct?> GetByKeyAsync(string key, CancellationToken cancellationToken = default);

    Task AddAsync(FertilizerProduct product, CancellationToken cancellationToken = default);

    Task UpdateAsync(FertilizerProduct product, CancellationToken cancellationToken = default);
}
