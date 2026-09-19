using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgrismartAPI.Infrastructure.Orders;

/// <summary>
/// Enforces the "unpaid orders are only held for 24 hours" policy (MONIME-INTEGRATION-README.md §2,
/// path 3): periodically cancels <see cref="PaymentMethod.UnpaidHold"/> orders whose hold has lapsed
/// without payment.
/// </summary>
public class OrderHoldExpiryService(IServiceScopeFactory scopeFactory, ILogger<OrderHoldExpiryService> logger) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(15);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        do
        {
            try
            {
                await CancelExpiredHoldsAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Failed to cancel expired unpaid-hold orders.");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task CancelExpiredHoldsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var now = DateTimeOffset.UtcNow;
        var expiredOrders = await dbContext.Orders
            .Where(order => order.PaymentMethod == PaymentMethod.UnpaidHold)
            .Where(order => order.PaymentStatus == PaymentStatus.Pending)
            .Where(order => order.Status == OrderStatus.Pending)
            .Where(order => order.HoldExpiresAt != null && order.HoldExpiresAt < now)
            .ToListAsync(cancellationToken);

        if (expiredOrders.Count == 0)
        {
            return;
        }

        foreach (var order in expiredOrders)
        {
            order.CancelUnpaid();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Cancelled {Count} unpaid-hold order(s) past their 24-hour hold window.", expiredOrders.Count);
    }
}
