using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Orders;

public class Order : Entity
{
    /// <summary>How long a <see cref="PaymentMethod.UnpaidHold"/> order's price/stock hold is honoured before it auto-cancels.</summary>
    public static readonly TimeSpan UnpaidHoldLifetime = TimeSpan.FromHours(24);

    public Guid UserId { get; private init; }
    public IReadOnlyList<OrderItem> Items { get; private init; }
    public decimal TotalAmount { get; private init; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private init; }

    public PaymentMethod PaymentMethod { get; private init; }

    /// <summary>
    /// Order-wide aggregate, derived from <see cref="SellerPayments"/>: <c>Paid</c> only once every
    /// seller's portion is paid, <c>Pending</c> if any seller's portion still is, else <c>Confirmed</c>.
    /// Persisted (recomputed on every mutation) so it stays queryable/filterable in SQL.
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; }

    /// <summary>
    /// One row per distinct seller among <see cref="Items"/> — a multi-vendor order's payment is
    /// reconciled seller-by-seller, never as one lump action a single seller could apply to everyone
    /// else's products too.
    /// </summary>
    public IReadOnlyList<SellerPayment> SellerPayments { get; private set; }

    /// <summary>Set only for <see cref="PaymentMethod.UnpaidHold"/> orders — when the price/stock hold lapses.</summary>
    public DateTimeOffset? HoldExpiresAt { get; private init; }

    /// <summary>Set once a Monime checkout session has been created for this order (<see cref="PaymentMethod.MonimeOnline"/> only).</summary>
    public Guid? CheckoutSessionId { get; private set; }

    /// <summary>Where the order is going. Null for orders placed before delivery details existed, or when the buyer collects.</summary>
    public string? DeliveryDistrict { get; private init; }

    public string? DeliveryAddress { get; private init; }

    public DateTimeOffset? ShippedAt { get; private set; }

    public DateTimeOffset? DeliveredAt { get; private set; }

    private Order(Guid userId, IReadOnlyList<OrderItem> items, PaymentMethod paymentMethod, DateTimeOffset? holdExpiresAt, string? deliveryDistrict, string? deliveryAddress)
    {
        DeliveryDistrict = deliveryDistrict;
        DeliveryAddress = deliveryAddress;
        UserId = userId;
        Items = items;
        TotalAmount = items.Sum(item => item.LineTotal);
        Status = OrderStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        PaymentMethod = paymentMethod;
        PaymentStatus = PaymentStatus.Pending;
        HoldExpiresAt = holdExpiresAt;
        SellerPayments = items.Select(item => item.SellerId).Distinct().Select(SellerPayment.CreatePending).ToList();
    }

    // EF Core
    private Order()
    {
        Items = new List<OrderItem>();
        SellerPayments = new List<SellerPayment>();
    }

    public static Order Create(Guid userId, IReadOnlyList<OrderItem> items, PaymentMethod paymentMethod, string? deliveryDistrict = null, string? deliveryAddress = null)
    {
        if (items.Count == 0)
        {
            throw new ArgumentException("An order must contain at least one item.", nameof(items));
        }

        if (items.Any(item => item.Quantity <= 0))
        {
            throw new ArgumentException("Item quantity must be greater than zero.", nameof(items));
        }

        var holdExpiresAt = paymentMethod == PaymentMethod.UnpaidHold
            ? DateTimeOffset.UtcNow.Add(UnpaidHoldLifetime)
            : (DateTimeOffset?)null;

        return new Order(userId, items, paymentMethod, holdExpiresAt, deliveryDistrict, deliveryAddress?.Trim());
    }

    /// <summary>
    /// Moves the order forward: Pending, Confirmed, Shipped, Delivered. Steps can be skipped but never undone.
    /// Unless the buyer pays in person, payment must come first, otherwise goods could leave unpaid and
    /// confirming an unpaid-hold order would stop its hold from expiring.
    /// </summary>
    public void AdvanceDelivery(OrderStatus next)
    {
        if (next is not (OrderStatus.Confirmed or OrderStatus.Shipped or OrderStatus.Delivered))
        {
            throw new InvalidOperationException("Status must be Confirmed, Shipped or Delivered.");
        }

        if (Status == OrderStatus.Cancelled)
        {
            throw new InvalidOperationException("A cancelled order cannot be delivered.");
        }

        if (next <= Status)
        {
            throw new InvalidOperationException($"This order is already {Status}.");
        }

        if (PaymentMethod != PaymentMethod.CashOnPickup && PaymentStatus == PaymentStatus.Pending)
        {
            throw new InvalidOperationException("This order has not been paid yet.");
        }

        var now = DateTimeOffset.UtcNow;
        if (next >= OrderStatus.Shipped)
        {
            ShippedAt ??= now;
        }

        if (next == OrderStatus.Delivered)
        {
            DeliveredAt = now;
        }

        Status = next;
    }

    /// <summary>Links this order to the Monime checkout session created for it. <see cref="PaymentMethod.MonimeOnline"/> only.</summary>
    public void AttachCheckoutSession(Guid checkoutSessionId)
    {
        if (PaymentMethod != PaymentMethod.MonimeOnline)
        {
            throw new InvalidOperationException("Only a Monime-paid order can be linked to a checkout session.");
        }

        CheckoutSessionId = checkoutSessionId;
    }

    public SellerPayment? GetSellerPayment(Guid sellerId) => SellerPayments.FirstOrDefault(payment => payment.SellerId == sellerId);

    /// <summary>A seller (or admin acting for them) confirms payment was received for their own products in this order — never another seller's.</summary>
    public void ConfirmSellerPayment(Guid sellerId, Guid userId)
    {
        var payment = GetSellerPayment(sellerId)
                      ?? throw new InvalidOperationException($"Seller '{sellerId}' has no products in this order.");

        if (payment.PaymentStatus != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending payment can be confirmed.");
        }

        ReplaceSellerPayment(payment with { PaymentStatus = PaymentStatus.Confirmed, ConfirmedAt = DateTimeOffset.UtcNow, ConfirmedByUserId = userId });
    }

    /// <summary>A seller (or admin acting for them) marks their own products in this order as fully paid/reconciled.</summary>
    public void MarkSellerPaid(Guid sellerId, Guid? userId)
    {
        var payment = GetSellerPayment(sellerId)
                      ?? throw new InvalidOperationException($"Seller '{sellerId}' has no products in this order.");

        if (payment.PaymentStatus == PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Payment is already reconciled as paid.");
        }

        ReplaceSellerPayment(payment with { PaymentStatus = PaymentStatus.Paid, ReconciledAt = DateTimeOffset.UtcNow, ReconciledByUserId = userId });
    }

    /// <summary>
    /// Marks every seller's portion paid at once — used only when a single external event covers the
    /// whole order (Monime charges the customer once for the entire cart), never triggered by one
    /// seller acting on another's behalf.
    /// </summary>
    public void MarkAllSellersPaid(Guid? userId)
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Payment is already reconciled as paid.");
        }

        var now = DateTimeOffset.UtcNow;
        SellerPayments = SellerPayments
            .Select(payment => payment.PaymentStatus == PaymentStatus.Paid
                ? payment
                : payment with { PaymentStatus = PaymentStatus.Paid, ReconciledAt = now, ReconciledByUserId = userId })
            .ToList();
        RecomputeAggregatePaymentStatus();
    }

    /// <summary>Cancels an order whose price/stock hold lapsed unpaid (<see cref="PaymentMethod.UnpaidHold"/>) or whose Monime session expired/was cancelled.</summary>
    public void CancelUnpaid()
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            throw new InvalidOperationException("A paid order cannot be cancelled as unpaid.");
        }

        Status = OrderStatus.Cancelled;
    }

    private void ReplaceSellerPayment(SellerPayment updated)
    {
        SellerPayments = SellerPayments.Select(payment => payment.SellerId == updated.SellerId ? updated : payment).ToList();
        RecomputeAggregatePaymentStatus();
    }

    private void RecomputeAggregatePaymentStatus()
    {
        PaymentStatus = SellerPayments.All(payment => payment.PaymentStatus == PaymentStatus.Paid) ? PaymentStatus.Paid
            : SellerPayments.Any(payment => payment.PaymentStatus == PaymentStatus.Pending) ? PaymentStatus.Pending
            : PaymentStatus.Confirmed;
    }
}
