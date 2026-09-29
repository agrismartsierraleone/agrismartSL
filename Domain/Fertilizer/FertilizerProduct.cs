using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Fertilizer;

/// <summary>A fertilizer a farmer can buy, described by its guaranteed analysis (% by weight).</summary>
public class FertilizerProduct : Entity
{
    public const double DefaultBagSizeKg = 50;

    /// <summary>Stable lowercase key used in API requests, e.g. "npk-15-15-15".</summary>
    public string Key { get; private set; }

    public string Name { get; private set; }

    public FertilizerProductType Type { get; private set; }

    public double NitrogenPercent { get; private set; }

    public double PhosphatePercent { get; private set; }

    public double PotashPercent { get; private set; }

    public double? SulphurPercent { get; private set; }

    public double? ZincPercent { get; private set; }

    public double BagSizeKg { get; private set; }

    /// <summary>Typical price of one bag, in the configured currency (SLE). Null when unknown.</summary>
    public decimal? PricePerBag { get; private set; }

    public string? Notes { get; private set; }

    private FertilizerProduct(string key)
    {
        Key = key;
        Name = null!;
    }

    // EF Core
    private FertilizerProduct()
    {
        Key = null!;
        Name = null!;
    }

    public static FertilizerProduct Create(
        string key,
        string name,
        FertilizerProductType type,
        double nitrogenPercent,
        double phosphatePercent,
        double potashPercent,
        double? sulphurPercent = null,
        double? zincPercent = null,
        double bagSizeKg = DefaultBagSizeKg,
        decimal? pricePerBag = null,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Key is required.", nameof(key));
        }

        var product = new FertilizerProduct(key.Trim().ToLowerInvariant());
        product.Update(name, type, nitrogenPercent, phosphatePercent, potashPercent, sulphurPercent, zincPercent,
            bagSizeKg, pricePerBag, notes);
        return product;
    }

    public void Update(
        string name,
        FertilizerProductType type,
        double nitrogenPercent,
        double phosphatePercent,
        double potashPercent,
        double? sulphurPercent,
        double? zincPercent,
        double bagSizeKg,
        decimal? pricePerBag,
        string? notes)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Name is required.", nameof(name));
        }

        if (!IsPercent(nitrogenPercent) || !IsPercent(phosphatePercent) || !IsPercent(potashPercent) ||
            (sulphurPercent is not null && !IsPercent(sulphurPercent.Value)) ||
            (zincPercent is not null && !IsPercent(zincPercent.Value)))
        {
            throw new ArgumentOutOfRangeException(nameof(nitrogenPercent), "Nutrient percentages must be between 0 and 100.");
        }

        if (nitrogenPercent + phosphatePercent + potashPercent <= 0)
        {
            throw new ArgumentException("A fertilizer must contain at least one of N, P2O5 or K2O.", nameof(nitrogenPercent));
        }

        if (nitrogenPercent + phosphatePercent + potashPercent > 100)
        {
            throw new ArgumentException("N + P2O5 + K2O cannot exceed 100%.", nameof(nitrogenPercent));
        }

        if (bagSizeKg <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bagSizeKg), "Bag size must be positive.");
        }

        if (pricePerBag < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(pricePerBag), "Price cannot be negative.");
        }

        Name = name.Trim();
        Type = type;
        NitrogenPercent = nitrogenPercent;
        PhosphatePercent = phosphatePercent;
        PotashPercent = potashPercent;
        SulphurPercent = sulphurPercent;
        ZincPercent = zincPercent;
        BagSizeKg = bagSizeKg;
        PricePerBag = pricePerBag;
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();
    }

    private static bool IsPercent(double value) => value is >= 0 and <= 100;
}
