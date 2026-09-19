using AgrismartAPI.Application.Orders;
using AgrismartAPI.Domain.Orders;

namespace AgrismartAPI.Application.Checkout;

/// <summary>Optional overrides for where Monime redirects the browser; falls back to configured defaults when omitted.</summary>
public record CreateCheckoutSessionRequest(string? SuccessUrl = null, string? CancelUrl = null);

/// <summary>Thin, customer-facing view of a Monime checkout session — just enough to redirect and show status.</summary>
/// <param name="UssdCode">
/// The real, per-transaction USSD dial string (e.g. "*715*18284#") from a Monime payment code created
/// alongside this session — not a static merchant code. Null if that create call failed; the redirect
/// flow (<paramref name="RedirectUrl"/>) still works either way.
/// </param>
public record CheckoutSessionDto(
    Guid Id,
    Guid OrderId,
    string MonimeSessionId,
    string Status,
    string? RedirectUrl,
    DateTimeOffset? ExpireTime,
    decimal Amount,
    string Currency,
    string? UssdCode,
    DateTimeOffset? UssdCodeExpireTime);

/// <summary>Admin-only, full view of a checkout session including the raw Monime payload and its attempt history.</summary>
public record CheckoutSessionAdminDto(
    Guid Id,
    Guid OrderId,
    Guid UserId,
    string MonimeSessionId,
    string Status,
    string? RedirectUrl,
    string? CancelUrl,
    string? SuccessUrl,
    DateTimeOffset? ExpireTime,
    string IdempotencyKey,
    string RawResponseJson,
    string? PaymentCodeId,
    string? UssdCode,
    string? PaymentCodeStatus,
    DateTimeOffset? PaymentCodeExpireTime,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSyncedAt,
    IReadOnlyList<CheckoutAuditLogDto> AuditLogs);

public record CheckoutAuditLogDto(
    Guid Id,
    Guid? OrderId,
    Guid? UserId,
    string IdempotencyKey,
    string RequestJson,
    int? ResponseStatusCode,
    string? ResponseJson,
    bool Success,
    string? ErrorMessage,
    Guid? CheckoutSessionId,
    DateTimeOffset CreatedAt);

/// <summary>
/// One row in the reconciliation view — an order's payment state, regardless of which of the three
/// checkout paths created it. <see cref="PaymentStatus"/> is the order-wide aggregate (every seller's
/// portion paid); <see cref="SellerPayments"/> is the actual per-seller breakdown a reconciliation
/// action operates on. An Admin sees every seller's row here; a Seller sees only their own.
/// </summary>
public record OrderPaymentDto(
    Guid OrderId,
    OrderCustomerDto Customer,
    decimal TotalAmount,
    PaymentMethod PaymentMethod,
    PaymentStatus PaymentStatus,
    OrderStatus OrderStatus,
    DateTimeOffset? HoldExpiresAt,
    Guid? CheckoutSessionId,
    string? MonimeSessionId,
    string? MonimeStatus,
    string? UssdCode,
    string? PaymentCodeStatus,
    IReadOnlyList<SellerPaymentDto> SellerPayments,
    DateTimeOffset CreatedAt);

/// <summary>One seller's own reconciliation state for their products within an order — never another seller's.</summary>
public record SellerPaymentDto(
    Guid SellerId,
    string SellerName,
    decimal Subtotal,
    PaymentStatus PaymentStatus,
    DateTimeOffset? ConfirmedAt,
    Guid? ConfirmedByUserId,
    DateTimeOffset? ReconciledAt,
    Guid? ReconciledByUserId);
