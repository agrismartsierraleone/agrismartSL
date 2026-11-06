namespace AgrismartAPI.Domain.Market;

public interface IMarketPriceRepository
{
    /// <summary>Prices dated from <paramref name="from"/> to <paramref name="to"/>, newest first.</summary>
    Task<IReadOnlyList<MarketPrice>> GetBetweenAsync(DateOnly from, DateOnly to, MarketPriceCategory? category = null, string? crop = null, string? district = null, CancellationToken cancellationToken = default);

    Task<MarketPrice?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Saves the new prices along with any changes to prices already loaded, all or nothing.</summary>
    Task SaveAsync(IReadOnlyList<MarketPrice> newPrices, CancellationToken cancellationToken = default);

    Task DeleteAsync(MarketPrice price, CancellationToken cancellationToken = default);
}
