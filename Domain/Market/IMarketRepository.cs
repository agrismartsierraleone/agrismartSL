namespace AgrismartAPI.Domain.Market;

public interface IMarketRepository
{
    Task<IReadOnlyList<MarketProduct>> GetAllAsync(Guid? categoryId, CancellationToken cancellationToken = default);

    Task<MarketProduct?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> ExistsForCategoryAsync(Guid categoryId, CancellationToken cancellationToken = default);

    Task<bool> ExistsForSellerAsync(Guid sellerId, CancellationToken cancellationToken = default);

    Task AddAsync(MarketProduct product, CancellationToken cancellationToken = default);

    Task UpdateAsync(MarketProduct product, CancellationToken cancellationToken = default);

    Task DeleteAsync(MarketProduct product, CancellationToken cancellationToken = default);
}
