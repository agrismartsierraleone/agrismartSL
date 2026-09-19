using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Orders;
using AgrismartAPI.Domain.Carts;
using AgrismartAPI.Domain.Checkout;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Users;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Checkout;

/// <summary>
/// The Monime hosted-checkout integration (path 1 of the three checkout flows — see
/// MONIME-INTEGRATION-README.md §2) plus payment reconciliation across all three flows. An Admin can
/// act on any seller's row on any order; a Seller can act only on their own row, on orders containing
/// at least one of their own products — never another seller's, even within the same multi-vendor order.
/// </summary>
public class CheckoutAppService(
    ICartRepository cartRepository,
    IOrderRepository orderRepository,
    OrderAppService orderAppService,
    ICheckoutSessionRepository checkoutSessionRepository,
    ICheckoutAuditLogRepository checkoutAuditLogRepository,
    IMonimeCheckoutClient monimeClient,
    IUserRepository userRepository,
    ISellerRepository sellerRepository,
    IOptions<MonimeSettings> monimeOptions)
{
    private readonly MonimeSettings _monime = monimeOptions.Value;

    /// <summary>Matches Monime's checkout-session default expiry (~1h) — well past the payment-code API's own 10-minute default, which would expire long before the redirect page does.</summary>
    private const string PaymentCodeDuration = "1h";

    /// <summary>Creates a Monime checkout session for the caller's current cart, placing the underlying (unpaid) order first.</summary>
    public async Task<CheckoutSessionDto> CreateSessionAsync(Guid userId, CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        var cart = await cartRepository.GetByUserIdAsync(userId, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            throw new ValidationException("Your cart is empty.");
        }

        var orderItems = cart.Items.Select(item => new OrderItemRequest(item.ProductId, item.Quantity)).ToList();
        var order = await orderAppService.CreateMonimeOrderAsync(userId, orderItems, cancellationToken);

        var idempotencyKey = Guid.NewGuid().ToString();
        var lineItems = order.Items
            .Select(item => (item.ProductName, ToMinorUnits(item.UnitPrice), item.Quantity, (string?)item.ProductId.ToString()))
            .ToList();

        try
        {
            var callResult = await monimeClient.CreateSessionAsync(
                "Agrismart Marketplace Order",
                lineItems,
                reference: order.Id.ToString(),
                callbackState: order.Id.ToString(),
                successUrl: request.SuccessUrl,
                cancelUrl: request.CancelUrl,
                idempotencyKey,
                cancellationToken);

            var checkoutSession = CheckoutSession.Create(
                order.Id,
                userId,
                callResult.Session.MonimeSessionId,
                callResult.Session.Status,
                callResult.Session.RedirectUrl,
                callResult.Session.CancelUrl,
                callResult.Session.SuccessUrl,
                callResult.Session.ExpireTime,
                idempotencyKey,
                callResult.ResponseJson);
            await checkoutSessionRepository.AddAsync(checkoutSession, cancellationToken);

            await TryAttachPaymentCodeAsync(checkoutSession, order, userId, cancellationToken);

            var orderEntity = await orderRepository.GetByIdAsync(order.Id, cancellationToken)
                               ?? throw new InvalidOperationException("Order disappeared immediately after being created.");
            orderEntity.AttachCheckoutSession(checkoutSession.Id);
            await orderRepository.UpdateAsync(orderEntity, cancellationToken);

            cart.Clear();
            await cartRepository.UpdateAsync(cart, cancellationToken);

            await checkoutAuditLogRepository.AddAsync(
                CheckoutAuditLog.Create(order.Id, userId, idempotencyKey, callResult.RequestJson, callResult.StatusCode, callResult.ResponseJson, success: true, errorMessage: null, checkoutSession.Id),
                cancellationToken);

            return ToDto(checkoutSession, order.TotalAmount);
        }
        catch (MonimeApiException ex)
        {
            // Compensate: the order was only ever a staging step for the Monime call. Remove it so a
            // retry (with the cart still intact) doesn't pile up orphan unpaid orders.
            var orphanOrder = await orderRepository.GetByIdAsync(order.Id, cancellationToken);
            if (orphanOrder is not null)
            {
                await orderRepository.DeleteAsync(orphanOrder, cancellationToken);
            }

            await checkoutAuditLogRepository.AddAsync(
                CheckoutAuditLog.Create(order.Id, userId, idempotencyKey, ex.RequestJson ?? string.Empty, ex.StatusCode, ex.ResponseJson, success: false, ex.Message, checkoutSessionId: null),
                cancellationToken);

            throw;
        }
    }

    /// <summary>Lets the caller poll their own session's status (e.g. while waiting for the hosted-page redirect to complete).</summary>
    public async Task<CheckoutSessionDto> GetSessionAsync(Guid userId, Guid checkoutSessionId, CancellationToken cancellationToken = default)
    {
        var session = await checkoutSessionRepository.GetByIdAsync(checkoutSessionId, cancellationToken);
        if (session is null || session.UserId != userId)
        {
            throw new NotFoundException($"Checkout session '{checkoutSessionId}' was not found.");
        }

        var order = await orderRepository.GetByIdAsync(session.OrderId, cancellationToken);
        return ToDto(session, order?.TotalAmount ?? 0m);
    }

    /// <summary>
    /// Reconciliation view: every order's payment state, across all three checkout paths. An Admin
    /// (<paramref name="callerRole"/>) sees every seller's row on every order; a Seller sees only
    /// orders containing at least one of their own products, and only their own row within them —
    /// never another seller's, even in the same multi-vendor order.
    /// </summary>
    public async Task<IReadOnlyList<OrderPaymentDto>> GetTransactionsAsync(
        Guid callerId, UserRole callerRole, PaymentStatus? paymentStatus, PaymentMethod? paymentMethod, CancellationToken cancellationToken = default)
    {
        var sellerScope = await ResolveSellerScopeAsync(callerId, callerRole, cancellationToken);

        var orders = (await orderRepository.GetAllAsync(cancellationToken))
            .Where(order => paymentMethod is null || order.PaymentMethod == paymentMethod)
            .Where(order => sellerScope is null || order.Items.Any(item => item.SellerId == sellerScope))
            .Where(order => paymentStatus is null || MatchesPaymentStatus(order, sellerScope, paymentStatus.Value))
            .OrderByDescending(order => order.CreatedAt)
            .ToList();

        var userIds = orders.Select(order => order.UserId).Distinct().ToList();
        var users = (await userRepository.GetByIdsAsync(userIds, cancellationToken)).ToDictionary(user => user.Id);

        var sellersById = (await sellerRepository.GetAllAsync(cancellationToken)).ToDictionary(seller => seller.Id);
        var sessions = (await checkoutSessionRepository.GetAllAsync(cancellationToken)).ToDictionary(session => session.OrderId);

        return orders
            .Select(order => ToOrderPaymentDto(order, users.GetValueOrDefault(order.UserId), sessions.GetValueOrDefault(order.Id), sellerScope, sellersById))
            .ToList();
    }

    /// <summary>
    /// A seller confirms payment was received for their own products in this order (e.g. cash handed
    /// over at pickup) — Pending → Confirmed. Never touches another seller's portion of the same order.
    /// An Admin acts the same way but must say which seller's portion via <paramref name="sellerId"/>
    /// when the order spans more than one.
    /// </summary>
    public async Task<OrderPaymentDto> ConfirmPaymentAsync(Guid callerId, UserRole callerRole, Guid orderId, Guid? sellerId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new NotFoundException($"Order '{orderId}' was not found.");

        var targetSellerId = await ResolveTargetSellerIdAsync(order, callerId, callerRole, sellerId, cancellationToken);
        var payment = order.GetSellerPayment(targetSellerId)!;

        if (payment.PaymentStatus != PaymentStatus.Pending)
        {
            throw new ConflictException($"This seller's payment on order '{orderId}' is already {payment.PaymentStatus}.");
        }

        order.ConfirmSellerPayment(targetSellerId, callerId);
        await orderRepository.UpdateAsync(order, cancellationToken);

        return await BuildOrderPaymentDtoAsync(order, callerId, callerRole, cancellationToken);
    }

    /// <summary>
    /// A seller marks their own products in this order as fully paid/reconciled — final step for
    /// cash-on-pickup/unpaid-hold orders. Never touches another seller's portion of the same order. An
    /// Admin acts the same way but must say which seller's portion via <paramref name="sellerId"/> when
    /// the order spans more than one.
    /// </summary>
    public async Task<OrderPaymentDto> MarkPaidAsync(Guid callerId, UserRole callerRole, Guid orderId, Guid? sellerId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new NotFoundException($"Order '{orderId}' was not found.");

        var targetSellerId = await ResolveTargetSellerIdAsync(order, callerId, callerRole, sellerId, cancellationToken);
        var payment = order.GetSellerPayment(targetSellerId)!;

        if (payment.PaymentStatus == PaymentStatus.Paid)
        {
            throw new ConflictException($"This seller's payment on order '{orderId}' is already marked as paid.");
        }

        order.MarkSellerPaid(targetSellerId, callerId);
        await orderRepository.UpdateAsync(order, cancellationToken);

        return await BuildOrderPaymentDtoAsync(order, callerId, callerRole, cancellationToken);
    }

    /// <summary>
    /// Monime-only: re-fetches the session from Monime and reconciles this order against it. Monime
    /// charges the customer once for the whole cart, so a completed session marks every seller's
    /// portion paid at once — this isn't one seller reconciling another's products, it's an external
    /// fact applying to the order as a whole. Needed because Monime's checkout_session webhook events
    /// are not yet stable — see MONIME-INTEGRATION-README.md §11. Callable by an Admin or a Seller with
    /// an item in this order.
    /// </summary>
    public async Task<OrderPaymentDto> SyncMonimeSessionAsync(Guid callerId, UserRole callerRole, Guid orderId, CancellationToken cancellationToken = default)
    {
        var order = await orderRepository.GetByIdAsync(orderId, cancellationToken)
                    ?? throw new NotFoundException($"Order '{orderId}' was not found.");

        var sellerScope = await ResolveSellerScopeAsync(callerId, callerRole, cancellationToken);
        if (sellerScope is not null && order.Items.All(item => item.SellerId != sellerScope))
        {
            throw new ForbiddenException("You can only reconcile orders containing your own products.");
        }

        if (order.PaymentMethod != PaymentMethod.MonimeOnline || order.CheckoutSessionId is not { } checkoutSessionId)
        {
            throw new ValidationException("This order has no Monime checkout session to sync.");
        }

        var session = await checkoutSessionRepository.GetByIdAsync(checkoutSessionId, cancellationToken)
                      ?? throw new NotFoundException($"Checkout session '{checkoutSessionId}' was not found.");

        var sessionCall = await monimeClient.GetSessionAsync(session.MonimeSessionId, cancellationToken);
        session.UpdateStatus(sessionCall.Session.Status, sessionCall.ResponseJson);

        // The customer could have paid via either the hosted redirect OR the USSD payment code — check
        // both channels, since either one completing means the order is paid.
        var paymentCodeStatus = session.PaymentCodeStatus;
        if (session.PaymentCodeId is { } paymentCodeId)
        {
            try
            {
                var codeCall = await monimeClient.GetPaymentCodeAsync(paymentCodeId, cancellationToken);
                session.UpdatePaymentCodeStatus(codeCall.PaymentCode.Status);
                paymentCodeStatus = codeCall.PaymentCode.Status;
            }
            catch (MonimeApiException)
            {
                // Non-fatal: fall back to the session's own status, refresh the payment code next sync.
            }
        }

        await checkoutSessionRepository.UpdateAsync(session, cancellationToken);

        var isPaid = sessionCall.Session.Status == "completed" || paymentCodeStatus == "completed";
        var bothChannelsDead = sessionCall.Session.Status is "cancelled" or "expired"
                               && paymentCodeStatus is null or "cancelled" or "expired";

        if (isPaid && order.PaymentStatus != PaymentStatus.Paid)
        {
            order.MarkAllSellersPaid(userId: null);
            await orderRepository.UpdateAsync(order, cancellationToken);

            // Best-effort: stop whichever channel didn't complete, so it can't be used for a second payment.
            if (sessionCall.Session.Status != "completed")
            {
                await TryCancelSessionAsync(session.MonimeSessionId, cancellationToken);
            }

            if (paymentCodeStatus != "completed" && session.PaymentCodeId is { } stillOpenCodeId)
            {
                await TryCancelPaymentCodeAsync(stillOpenCodeId, cancellationToken);
            }
        }
        else if (bothChannelsDead && order.Status != OrderStatus.Cancelled && order.PaymentStatus != PaymentStatus.Paid)
        {
            order.CancelUnpaid();
            await orderRepository.UpdateAsync(order, cancellationToken);
        }

        return await BuildOrderPaymentDtoAsync(order, callerId, callerRole, cancellationToken);
    }

    /// <summary>
    /// Creates the dynamic, per-transaction USSD payment code for this session (see
    /// MONIME-INTEGRATION-README.md §2) — a fallback to the hosted redirect page, not a static
    /// merchant code. Non-fatal: the redirect flow works fine without it.
    /// </summary>
    private async Task TryAttachPaymentCodeAsync(CheckoutSession checkoutSession, OrderDto order, Guid userId, CancellationToken cancellationToken)
    {
        var paymentCodeIdempotencyKey = Guid.NewGuid().ToString();
        try
        {
            var codeCall = await monimeClient.CreatePaymentCodeAsync(
                $"Agrismart order {order.Id}",
                ToMinorUnits(order.TotalAmount),
                PaymentCodeDuration,
                reference: order.Id.ToString(),
                paymentCodeIdempotencyKey,
                cancellationToken);

            checkoutSession.AttachPaymentCode(codeCall.PaymentCode.PaymentCodeId, codeCall.PaymentCode.UssdCode, codeCall.PaymentCode.Status, codeCall.PaymentCode.ExpireTime);
            await checkoutSessionRepository.UpdateAsync(checkoutSession, cancellationToken);

            await checkoutAuditLogRepository.AddAsync(
                CheckoutAuditLog.Create(order.Id, userId, paymentCodeIdempotencyKey, codeCall.RequestJson, codeCall.StatusCode, codeCall.ResponseJson, success: true, errorMessage: null, checkoutSession.Id),
                cancellationToken);
        }
        catch (MonimeApiException ex)
        {
            await checkoutAuditLogRepository.AddAsync(
                CheckoutAuditLog.Create(order.Id, userId, paymentCodeIdempotencyKey, ex.RequestJson ?? string.Empty, ex.StatusCode, ex.ResponseJson, success: false, ex.Message, checkoutSession.Id),
                cancellationToken);
        }
    }

    private async Task TryCancelSessionAsync(string monimeSessionId, CancellationToken cancellationToken)
    {
        try
        {
            await monimeClient.CancelSessionAsync(monimeSessionId, cancellationToken);
        }
        catch (MonimeApiException)
        {
            // Best-effort cleanup only — Monime rejects this once the session has been initiated anyway.
        }
    }

    private async Task TryCancelPaymentCodeAsync(string paymentCodeId, CancellationToken cancellationToken)
    {
        try
        {
            await monimeClient.CancelPaymentCodeAsync(paymentCodeId, cancellationToken);
        }
        catch (MonimeApiException)
        {
            // Best-effort cleanup only — Monime rejects this once the code has been used anyway.
        }
    }

    public async Task<IReadOnlyList<CheckoutSessionAdminDto>> GetSessionsAdminAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await checkoutSessionRepository.GetAllAsync(cancellationToken);
        var result = new List<CheckoutSessionAdminDto>(sessions.Count);
        foreach (var session in sessions)
        {
            result.Add(await ToAdminDtoAsync(session, cancellationToken));
        }

        return result;
    }

    public async Task<CheckoutSessionAdminDto> GetSessionAdminAsync(Guid checkoutSessionId, CancellationToken cancellationToken = default)
    {
        var session = await checkoutSessionRepository.GetByIdAsync(checkoutSessionId, cancellationToken)
                      ?? throw new NotFoundException($"Checkout session '{checkoutSessionId}' was not found.");

        return await ToAdminDtoAsync(session, cancellationToken);
    }

    /// <summary>Null for an Admin (unrestricted); the caller's own seller id for a Seller.</summary>
    private async Task<Guid?> ResolveSellerScopeAsync(Guid callerId, UserRole callerRole, CancellationToken cancellationToken)
    {
        if (callerRole != UserRole.Seller)
        {
            return null;
        }

        var seller = await sellerRepository.GetByUserIdAsync(callerId, cancellationToken)
                     ?? throw new NotFoundException("No seller profile is linked to this account.");
        return seller.Id;
    }

    /// <summary>
    /// Which seller's row a reconciliation action targets. A Seller can only ever target their own —
    /// <paramref name="requestedSellerId"/> is ignored for them. An Admin must disambiguate via
    /// <paramref name="requestedSellerId"/> when the order spans more than one seller. The returned id
    /// is always guaranteed to have a matching <see cref="Order.SellerPayments"/> row, so callers can
    /// safely assume <see cref="Order.GetSellerPayment"/> won't return null for it.
    /// </summary>
    private async Task<Guid> ResolveTargetSellerIdAsync(Order order, Guid callerId, UserRole callerRole, Guid? requestedSellerId, CancellationToken cancellationToken)
    {
        Guid targetSellerId;

        if (callerRole == UserRole.Seller)
        {
            var seller = await sellerRepository.GetByUserIdAsync(callerId, cancellationToken)
                         ?? throw new NotFoundException("No seller profile is linked to this account.");

            if (order.Items.All(item => item.SellerId != seller.Id))
            {
                throw new ForbiddenException("You can only reconcile orders containing your own products.");
            }

            targetSellerId = seller.Id;
        }
        else
        {
            var distinctSellerIds = order.Items.Select(item => item.SellerId).Distinct().ToList();
            if (requestedSellerId is { } sellerId)
            {
                if (!distinctSellerIds.Contains(sellerId))
                {
                    throw new ValidationException($"Seller '{sellerId}' has no products in this order.");
                }

                targetSellerId = sellerId;
            }
            else if (distinctSellerIds.Count == 1)
            {
                targetSellerId = distinctSellerIds[0];
            }
            else
            {
                throw new ValidationException("This order has products from more than one seller — specify which seller's payment to reconcile via ?sellerId=.");
            }
        }

        if (order.GetSellerPayment(targetSellerId) is null)
        {
            // The seller genuinely has products in this order (checked above), but the order predates
            // per-seller payment tracking (see MONIME-INTEGRATION-README.md §9) and was backfilled with
            // an empty SellerPayments list — there's nothing to reconcile it against.
            throw new ValidationException("This order predates per-seller payment tracking and cannot be reconciled here.");
        }

        return targetSellerId;
    }

    private async Task<OrderPaymentDto> BuildOrderPaymentDtoAsync(Order order, Guid callerId, UserRole callerRole, CancellationToken cancellationToken)
    {
        var user = await userRepository.GetByIdAsync(order.UserId, cancellationToken);
        var session = order.CheckoutSessionId is { } sessionId
            ? await checkoutSessionRepository.GetByIdAsync(sessionId, cancellationToken)
            : null;

        var sellerScope = await ResolveSellerScopeAsync(callerId, callerRole, cancellationToken);
        var relevantSellerIds = order.Items.Select(item => item.SellerId).Distinct();
        var sellersById = (await sellerRepository.GetAllAsync(cancellationToken))
            .Where(seller => relevantSellerIds.Contains(seller.Id))
            .ToDictionary(seller => seller.Id);

        return ToOrderPaymentDto(order, user, session, sellerScope, sellersById);
    }

    private async Task<CheckoutSessionAdminDto> ToAdminDtoAsync(CheckoutSession session, CancellationToken cancellationToken)
    {
        var auditLogs = await checkoutAuditLogRepository.GetByCheckoutSessionIdAsync(session.Id, cancellationToken);
        return new CheckoutSessionAdminDto(
            session.Id,
            session.OrderId,
            session.UserId,
            session.MonimeSessionId,
            session.Status,
            session.RedirectUrl,
            session.CancelUrl,
            session.SuccessUrl,
            session.ExpireTime,
            session.IdempotencyKey,
            session.RawResponseJson,
            session.PaymentCodeId,
            session.UssdCode,
            session.PaymentCodeStatus,
            session.PaymentCodeExpireTime,
            session.CreatedAt,
            session.LastSyncedAt,
            auditLogs.Select(ToDto).ToList());
    }

    /// <summary>Whichever <paramref name="filter"/> is being matched against: the seller's own row when scoped, else the order-wide aggregate.</summary>
    private static bool MatchesPaymentStatus(Order order, Guid? sellerScope, PaymentStatus filter)
    {
        if (sellerScope is { } sellerId)
        {
            return order.GetSellerPayment(sellerId)?.PaymentStatus == filter;
        }

        return order.PaymentStatus == filter;
    }

    /// <summary><paramref name="viewerSellerScope"/> null (Admin) includes every seller's row; a seller id restricts the breakdown to just that seller's own row.</summary>
    private static OrderPaymentDto ToOrderPaymentDto(Order order, User? user, CheckoutSession? session, Guid? viewerSellerScope, IReadOnlyDictionary<Guid, Seller> sellersById)
    {
        var sellerPayments = order.SellerPayments
            .Where(payment => viewerSellerScope is null || payment.SellerId == viewerSellerScope)
            .Select(payment => new SellerPaymentDto(
                payment.SellerId,
                sellersById.TryGetValue(payment.SellerId, out var seller) ? seller.Name : "Unknown",
                order.Items.Where(item => item.SellerId == payment.SellerId).Sum(item => item.LineTotal),
                payment.PaymentStatus,
                payment.ConfirmedAt,
                payment.ConfirmedByUserId,
                payment.ReconciledAt,
                payment.ReconciledByUserId))
            .ToList();

        return new OrderPaymentDto(
            order.Id,
            new OrderCustomerDto(order.UserId, user?.Name ?? "Unknown", user?.Email ?? "unknown"),
            order.TotalAmount,
            order.PaymentMethod,
            order.PaymentStatus,
            order.Status,
            order.HoldExpiresAt,
            order.CheckoutSessionId,
            session?.MonimeSessionId,
            session?.Status,
            session?.UssdCode,
            session?.PaymentCodeStatus,
            sellerPayments,
            order.CreatedAt);
    }

    private static CheckoutAuditLogDto ToDto(CheckoutAuditLog log) => new(
        log.Id,
        log.OrderId,
        log.UserId,
        log.IdempotencyKey,
        log.RequestJson,
        log.ResponseStatusCode,
        log.ResponseJson,
        log.Success,
        log.ErrorMessage,
        log.CheckoutSessionId,
        log.CreatedAt);

    private CheckoutSessionDto ToDto(CheckoutSession session, decimal amount) => new(
        session.Id,
        session.OrderId,
        session.MonimeSessionId,
        session.Status,
        session.RedirectUrl,
        session.ExpireTime,
        amount,
        _monime.Currency,
        session.UssdCode,
        session.PaymentCodeExpireTime);

    private static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100m, MidpointRounding.AwayFromZero);
}
