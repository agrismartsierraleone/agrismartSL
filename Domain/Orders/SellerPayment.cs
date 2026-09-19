namespace AgrismartAPI.Domain.Orders;

/// <summary>
/// One seller's own reconciliation state for their portion of an <see cref="Order"/>. An order can span
/// multiple sellers (a multi-vendor cart); each seller only ever confirms/reconciles their own row —
/// never another seller's — see <see cref="Order.ConfirmSellerPayment"/>/<see cref="Order.MarkSellerPaid"/>.
/// </summary>
public record SellerPayment(
    Guid SellerId,
    PaymentStatus PaymentStatus,
    DateTimeOffset? ConfirmedAt,
    Guid? ConfirmedByUserId,
    DateTimeOffset? ReconciledAt,
    Guid? ReconciledByUserId)
{
    public static SellerPayment CreatePending(Guid sellerId) => new(sellerId, PaymentStatus.Pending, null, null, null, null);
}
