namespace AgrismartAPI.Domain.Orders;

/// <summary>How a customer chose to settle an order — see MONIME-INTEGRATION-README.md §2 for the three flows.</summary>
public enum PaymentMethod
{
    /// <summary>Pay up front through Monime's hosted checkout (card/bank/momo, incl. Orange Money) before delivery.</summary>
    MonimeOnline,

    /// <summary>Pay in person (cash or in-app-selected provider) when the goods are collected.</summary>
    CashOnPickup,

    /// <summary>Order placed unpaid; price/stock are only held for <see cref="Order.UnpaidHoldLifetime"/>.</summary>
    UnpaidHold
}
