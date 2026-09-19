namespace AgrismartAPI.Domain.Orders;

/// <summary>Reconciliation state of an order's payment — independent of <see cref="OrderStatus"/> (fulfillment).</summary>
public enum PaymentStatus
{
    /// <summary>No payment has been received/confirmed yet.</summary>
    Pending,

    /// <summary>An admin (or Monime) confirmed payment was received, pending final reconciliation.</summary>
    Confirmed,

    /// <summary>Payment is fully reconciled/settled.</summary>
    Paid
}
