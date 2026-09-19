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
    public PaymentStatus PaymentStatus { get; private set; }

    /// <summary>Set only for <see cref="PaymentMethod.UnpaidHold"/> orders — when the price/stock hold lapses.</summary>
    public DateTimeOffset? HoldExpiresAt { get; private init; }

    /// <summary>Set once a Monime checkout session has been created for this order (<see cref="PaymentMethod.MonimeOnline"/> only).</summary>
    public Guid? CheckoutSessionId { get; private set; }

    public DateTimeOffset? ConfirmedAt { get; private set; }
    public Guid? ConfirmedByAdminId { get; private set; }

    public DateTimeOffset? ReconciledAt { get; private set; }
    public Guid? ReconciledByAdminId { get; private set; }

    private Order(Guid userId, IReadOnlyList<OrderItem> items, PaymentMethod paymentMethod, DateTimeOffset? holdExpiresAt)
    {
        UserId = userId;
        Items = items;
        TotalAmount = items.Sum(item => item.LineTotal);
        Status = OrderStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
        PaymentMethod = paymentMethod;
        PaymentStatus = PaymentStatus.Pending;
        HoldExpiresAt = holdExpiresAt;
    }

    // EF Core
    private Order()
    {
        Items = new List<OrderItem>();
    }

    public static Order Create(Guid userId, IReadOnlyList<OrderItem> items, PaymentMethod paymentMethod)
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

        return new Order(userId, items, paymentMethod, holdExpiresAt);
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

    /// <summary>An admin confirms payment was received (e.g. cash handed over at pickup), pending final reconciliation.</summary>
    public void ConfirmPayment(Guid adminId)
    {
        if (PaymentStatus != PaymentStatus.Pending)
        {
            throw new InvalidOperationException("Only a pending payment can be confirmed.");
        }

        PaymentStatus = PaymentStatus.Confirmed;
        ConfirmedAt = DateTimeOffset.UtcNow;
        ConfirmedByAdminId = adminId;
    }

    /// <summary>Marks the order as fully paid/reconciled. <paramref name="adminId"/> is null when Monime itself confirmed payment.</summary>
    public void MarkPaid(Guid? adminId)
    {
        if (PaymentStatus == PaymentStatus.Paid)
        {
            throw new InvalidOperationException("Payment is already reconciled as paid.");
        }

        PaymentStatus = PaymentStatus.Paid;
        ReconciledAt = DateTimeOffset.UtcNow;
        ReconciledByAdminId = adminId;
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
}
