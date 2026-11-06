using System.Globalization;
using System.Text.RegularExpressions;
using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Market;

/// <summary>The price of a crop in one district on one day. There is one per crop, district and date.</summary>
public class MarketPrice : Entity
{
    public const int MaxCropLength = 100;
    public const int MaxUnitLength = 50;

    public MarketPriceCategory Category { get; private init; }

    /// <summary>The name of the crop, fertilizer or seed.</summary>
    public string Crop { get; private init; }
    public string District { get; private init; }
    public DateOnly PriceDate { get; private init; }
    public decimal Price { get; private set; }
    public string Unit { get; private set; }

    /// <summary>Null once the admin who recorded the price has been deleted. The price itself stays.</summary>
    public Guid? RecordedByUserId { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    private MarketPrice(MarketPriceCategory category, string crop, string district, DateOnly priceDate, decimal price, string unit, Guid recordedByUserId)
    {
        Category = category;
        Crop = crop;
        District = district;
        PriceDate = priceDate;
        Price = price;
        Unit = unit;
        RecordedByUserId = recordedByUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private MarketPrice()
    {
        Crop = null!;
        District = null!;
        Unit = null!;
    }

    public static MarketPrice Create(MarketPriceCategory category, string crop, string district, DateOnly priceDate, decimal price, string unit, Guid recordedByUserId) =>
        new(category, NormalizeCrop(crop), district, priceDate, price, unit.Trim(), recordedByUserId);

    public void Update(decimal price, string unit, Guid recordedByUserId)
    {
        Price = price;
        Unit = unit.Trim();
        RecordedByUserId = recordedByUserId;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Gives "maize", " MAIZE " and "Maize" the same spelling, so they count as one crop.</summary>
    public static string NormalizeCrop(string crop)
    {
        var titleCase = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(Regex.Replace(crop.Trim().ToLowerInvariant(), @"\s+", " "));
        return Abbreviations.Replace(titleCase, match => match.Value.ToUpperInvariant());
    }

    /// <summary>Fertilizer and seed names that are written in capitals, such as NPK 15-15-15 or NERICA 4.</summary>
    private static readonly Regex Abbreviations = new(@"\b(Npk|Dap|Mop|Tsp|Ssp|Can|Nerica)\b", RegexOptions.Compiled);
}
