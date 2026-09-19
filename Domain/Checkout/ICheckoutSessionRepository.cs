namespace AgrismartAPI.Domain.Checkout;

public interface ICheckoutSessionRepository
{
    Task<IReadOnlyList<CheckoutSession>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<CheckoutSession?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<CheckoutSession?> GetByOrderIdAsync(Guid orderId, CancellationToken cancellationToken = default);

    Task AddAsync(CheckoutSession session, CancellationToken cancellationToken = default);

    Task UpdateAsync(CheckoutSession session, CancellationToken cancellationToken = default);
}
