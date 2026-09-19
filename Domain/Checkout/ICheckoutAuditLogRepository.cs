namespace AgrismartAPI.Domain.Checkout;

public interface ICheckoutAuditLogRepository
{
    Task AddAsync(CheckoutAuditLog auditLog, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckoutAuditLog>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CheckoutAuditLog>> GetByCheckoutSessionIdAsync(Guid checkoutSessionId, CancellationToken cancellationToken = default);
}
