using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Checkout;

/// <summary>One row per Monime create-session attempt (success or failure) — see MONIME-INTEGRATION-README.md §10.</summary>
public class CheckoutAuditLog : Entity
{
    public Guid? OrderId { get; private init; }
    public Guid? UserId { get; private init; }
    public string IdempotencyKey { get; private init; }
    public string RequestJson { get; private init; }
    public int? ResponseStatusCode { get; private init; }
    public string? ResponseJson { get; private init; }
    public bool Success { get; private init; }
    public string? ErrorMessage { get; private init; }
    public Guid? CheckoutSessionId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private CheckoutAuditLog(
        Guid? orderId,
        Guid? userId,
        string idempotencyKey,
        string requestJson,
        int? responseStatusCode,
        string? responseJson,
        bool success,
        string? errorMessage,
        Guid? checkoutSessionId)
    {
        OrderId = orderId;
        UserId = userId;
        IdempotencyKey = idempotencyKey;
        RequestJson = requestJson;
        ResponseStatusCode = responseStatusCode;
        ResponseJson = responseJson;
        Success = success;
        ErrorMessage = errorMessage;
        CheckoutSessionId = checkoutSessionId;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private CheckoutAuditLog()
    {
        IdempotencyKey = null!;
        RequestJson = null!;
    }

    public static CheckoutAuditLog Create(
        Guid? orderId,
        Guid? userId,
        string idempotencyKey,
        string requestJson,
        int? responseStatusCode,
        string? responseJson,
        bool success,
        string? errorMessage,
        Guid? checkoutSessionId) =>
        new(orderId, userId, idempotencyKey, requestJson, responseStatusCode, responseJson, success, errorMessage, checkoutSessionId);
}
