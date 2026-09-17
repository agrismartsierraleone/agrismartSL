namespace AgrismartAPI.Domain.Carts;

public interface ICartRepository
{
    Task<Cart?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(Cart cart, CancellationToken cancellationToken = default);

    Task UpdateAsync(Cart cart, CancellationToken cancellationToken = default);
}
