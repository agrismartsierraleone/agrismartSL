using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Logistics;
using AgrismartAPI.Domain.Logistics;
using AgrismartAPI.Domain.Market;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Market;

public class MarketPriceAppService(
    IMarketPriceRepository marketPriceRepository,
    ICurrentUserService currentUser,
    IOptions<DeliveryOptions> deliveryOptions,
    IMarketPricePdfRenderer pdfRenderer,
    TimeProvider timeProvider)
{
    /// <summary>An older price is no longer a current price, so it drops off the board.</summary>
    public const int BoardLookbackDays = 30;

    /// <summary>Callers that send no category get crops, so a client written before categories keeps its meaning.</summary>
    private const MarketPriceCategory DefaultCategory = MarketPriceCategory.Crop;

    private const int TickerMaxAgeDays = 7;
    private const int TickerMaxItems = 60;
    private const int MaxPricesPerRequest = 200;
    private const int MaxHistoryDays = 365;
    private const decimal MaxPrice = 1_000_000_000m;

    private DateOnly Today => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    private string Currency => deliveryOptions.Value.Currency;

    public async Task<MarketPriceBoardDto> GetBoardAsync(MarketPriceCategory? category, string? crop, string? district, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var asOf = date ?? Today;
        var items = await GetLatestAsync(asOf, category ?? DefaultCategory, CleanCrop(crop), CleanDistrict(district), cancellationToken);
        return new MarketPriceBoardDto(Currency, asOf, items.OrderBy(item => item.Crop).ThenBy(item => item.District).ToList());
    }

    public async Task<MarketPricePdfDto> GetBoardPdfAsync(MarketPriceCategory? category, string? crop, string? district, DateOnly? date, CancellationToken cancellationToken = default)
    {
        var selected = category ?? DefaultCategory;
        var board = await GetBoardAsync(selected, crop, district, date, cancellationToken);
        var content = pdfRenderer.Render(board, selected, CleanCrop(crop), CleanDistrict(district));
        return new MarketPricePdfDto(content, $"agrismart-{selected.ToString().ToLowerInvariant()}-prices-{board.AsOf:yyyy-MM-dd}.pdf");
    }

    public async Task<MarketPriceBoardDto> GetTickerAsync(CancellationToken cancellationToken = default)
    {
        var today = Today;
        var oldest = today.AddDays(1 - TickerMaxAgeDays);
        var items = await GetLatestAsync(today, null, null, null, cancellationToken);

        return new MarketPriceBoardDto(
            Currency,
            today,
            items
                .Where(item => item.PriceDate >= oldest)
                .OrderByDescending(item => item.PriceDate)
                .ThenBy(item => item.Crop)
                .ThenBy(item => item.District)
                .Take(TickerMaxItems)
                .ToList());
    }

    public async Task<MarketPriceHistoryDto> GetHistoryAsync(MarketPriceCategory? category, string? crop, string? district, int days, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(crop))
        {
            throw Invalid("crop", "crop is required.");
        }

        if (string.IsNullOrWhiteSpace(district))
        {
            throw Invalid("district", "district is required.");
        }

        if (days is < 1 or > MaxHistoryDays)
        {
            throw Invalid("days", $"days must be between 1 and {MaxHistoryDays}.");
        }

        var today = Today;
        var cleanCrop = MarketPrice.NormalizeCrop(crop);
        var cleanDistrict = RequireDistrict(district, "district");
        var selected = category ?? DefaultCategory;
        var prices = await marketPriceRepository.GetBetweenAsync(today.AddDays(1 - days), today, selected, cleanCrop, cleanDistrict, cancellationToken);

        return new MarketPriceHistoryDto(
            selected,
            cleanCrop,
            cleanDistrict,
            Currency,
            prices.OrderBy(price => price.PriceDate).Select(price => new MarketPricePointDto(price.PriceDate, price.Price, price.Unit)).ToList());
    }

    /// <summary>Saves every price or none. A price already recorded for that crop, district and day is replaced.</summary>
    public async Task<RecordMarketPricesResultDto> RecordAsync(RecordMarketPricesRequest request, CancellationToken cancellationToken = default)
    {
        var today = Today;
        var priceDate = request.PriceDate ?? today;
        if (priceDate > today)
        {
            throw Invalid("priceDate", "priceDate cannot be in the future.");
        }

        var entries = request.Prices ?? [];
        if (entries.Count is 0 or > MaxPricesPerRequest)
        {
            throw Invalid("prices", $"Send between 1 and {MaxPricesPerRequest} prices.");
        }

        var cleaned = entries.Select(Clean).ToList();
        var duplicate = cleaned.GroupBy(entry => (entry.Category, entry.Crop, entry.District)).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw Invalid("prices", $"{duplicate.Key.Crop} in {duplicate.Key.District} appears more than once.");
        }

        var existing = (await marketPriceRepository.GetBetweenAsync(priceDate, priceDate, cancellationToken: cancellationToken))
            .ToDictionary(price => (price.Category, price.Crop, price.District));

        var newPrices = new List<MarketPrice>();
        foreach (var entry in cleaned)
        {
            if (existing.TryGetValue((entry.Category, entry.Crop, entry.District), out var price))
            {
                price.Update(entry.Price, entry.Unit, currentUser.UserId);
            }
            else
            {
                newPrices.Add(MarketPrice.Create(entry.Category, entry.Crop, entry.District, priceDate, entry.Price, entry.Unit, currentUser.UserId));
            }
        }

        await marketPriceRepository.SaveAsync(newPrices, cancellationToken);
        return new RecordMarketPricesResultDto(priceDate, newPrices.Count, cleaned.Count - newPrices.Count);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var price = await marketPriceRepository.GetAsync(id, cancellationToken)
                    ?? throw new NotFoundException($"Market price '{id}' was not found.");

        await marketPriceRepository.DeleteAsync(price, cancellationToken);
    }

    private async Task<IReadOnlyList<MarketPriceDto>> GetLatestAsync(DateOnly asOf, MarketPriceCategory? category, string? crop, string? district, CancellationToken cancellationToken)
    {
        var prices = await marketPriceRepository.GetBetweenAsync(asOf.AddDays(1 - BoardLookbackDays), asOf, category, crop, district, cancellationToken);

        return prices
            .GroupBy(price => (price.Category, price.Crop, price.District))
            .Select(group =>
            {
                var history = group.OrderByDescending(price => price.PriceDate).ToList();
                return ToDto(history[0], history.Skip(1));
            })
            .ToList();
    }

    private static MarketPriceDto ToDto(MarketPrice latest, IEnumerable<MarketPrice> earlier)
    {
        // A 50 kg bag and a cup cannot be compared, so only look back to a price in the same unit.
        var previous = earlier.FirstOrDefault(price => price.Unit.Equals(latest.Unit, StringComparison.OrdinalIgnoreCase));
        if (previous is null)
        {
            return new MarketPriceDto(latest.Id, latest.Category, latest.Crop, latest.District, latest.Price, latest.Unit, latest.PriceDate, null, null, null, null, PriceTrend.New);
        }

        var change = latest.Price - previous.Price;
        var trend = change switch
        {
            > 0 => PriceTrend.Up,
            < 0 => PriceTrend.Down,
            _ => PriceTrend.Flat
        };

        return new MarketPriceDto(
            latest.Id, latest.Category, latest.Crop, latest.District, latest.Price, latest.Unit, latest.PriceDate,
            previous.Price, previous.PriceDate, change, Math.Round(change / previous.Price * 100, 1), trend);
    }

    private static (MarketPriceCategory Category, string Crop, string District, decimal Price, string Unit) Clean(MarketPriceEntryRequest entry, int index)
    {
        var prefix = $"prices[{index}]";
        var category = entry.Category ?? DefaultCategory;
        if (!Enum.IsDefined(category))
        {
            throw Invalid($"{prefix}.category", $"{prefix}: category must be crop, fertilizer or seed.");
        }

        if (string.IsNullOrWhiteSpace(entry.Crop) || entry.Crop.Trim().Length > MarketPrice.MaxCropLength)
        {
            throw Invalid($"{prefix}.crop", $"{prefix}: crop is required and must be at most {MarketPrice.MaxCropLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(entry.Unit) || entry.Unit.Trim().Length > MarketPrice.MaxUnitLength)
        {
            throw Invalid($"{prefix}.unit", $"{prefix}: unit is required and must be at most {MarketPrice.MaxUnitLength} characters.");
        }

        var price = Math.Round(entry.Price ?? 0, 2);
        if (price is <= 0 or > MaxPrice)
        {
            throw Invalid($"{prefix}.price", $"{prefix}: price must be greater than 0 and at most {MaxPrice:0}.");
        }

        return (category, MarketPrice.NormalizeCrop(entry.Crop), RequireDistrict(entry.District, $"{prefix}.district"), price, entry.Unit.Trim());
    }

    private static string? CleanCrop(string? crop) =>
        string.IsNullOrWhiteSpace(crop) ? null : MarketPrice.NormalizeCrop(crop);

    private static string? CleanDistrict(string? district) =>
        string.IsNullOrWhiteSpace(district) ? null : RequireDistrict(district, "district");

    private static string RequireDistrict(string? value, string field) =>
        SierraLeoneDistricts.Find(value)?.Name ?? throw Invalid(field, $"{field} must be a Sierra Leone district.");

    private static ValidationException Invalid(string field, string error) =>
        new(error, new Dictionary<string, string[]> { [field] = [error] });
}
