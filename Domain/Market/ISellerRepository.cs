namespace AgrismartAPI.Domain.Market;

public interface ISellerRepository
{
    Task<IReadOnlyList<Seller>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Seller?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<Seller?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Seller seller, CancellationToken cancellationToken = default);

    Task UpdateAsync(Seller seller, CancellationToken cancellationToken = default);

    Task DeleteAsync(Seller seller, CancellationToken cancellationToken = default);
}
