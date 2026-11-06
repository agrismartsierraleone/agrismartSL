using AgrismartAPI.Domain.Market;

namespace AgrismartAPI.Application.Market;

public enum PriceTrend
{
    New,
    Up,
    Down,
    Flat
}

/// <param name="Crop">The name of the crop, fertilizer or seed.</param>
/// <param name="Category">Defaults to crop.</param>
public record MarketPriceEntryRequest(string? Crop, string? District, decimal? Price, string? Unit, MarketPriceCategory? Category = null);

/// <summary>Records several prices for one day. <c>PriceDate</c> defaults to today.</summary>
public record RecordMarketPricesRequest(DateOnly? PriceDate, IReadOnlyList<MarketPriceEntryRequest>? Prices);

public record RecordMarketPricesResultDto(DateOnly PriceDate, int Created, int Updated);

/// <summary>The latest price of a crop in a district, compared with the record before it.</summary>
/// <param name="PreviousPrice">Null when there is no earlier price in the same unit.</param>
/// <param name="ChangePercent">Rounded to one decimal place.</param>
public record MarketPriceDto(
    Guid Id,
    MarketPriceCategory Category,
    string Crop,
    string District,
    decimal Price,
    string Unit,
    DateOnly PriceDate,
    decimal? PreviousPrice,
    DateOnly? PreviousDate,
    decimal? Change,
    decimal? ChangePercent,
    PriceTrend Trend);

public record MarketPriceBoardDto(string Currency, DateOnly AsOf, IReadOnlyList<MarketPriceDto> Items);

public record MarketPricePdfDto(byte[] Content, string FileName);

public record MarketPricePointDto(DateOnly Date, decimal Price, string Unit);

public record MarketPriceHistoryDto(MarketPriceCategory Category, string Crop, string District, string Currency, IReadOnlyList<MarketPricePointDto> Points);
