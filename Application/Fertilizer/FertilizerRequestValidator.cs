using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Fertilizer;

namespace AgrismartAPI.Application.Fertilizer;

/// <summary>
/// Field-level validation of a <see cref="FertilizerCalculationRequest"/>. Collects every problem so
/// the app can highlight all bad fields at once; keys are camelCase JSON paths.
/// </summary>
public static class FertilizerRequestValidator
{
    public const double MinPh = 3.0;
    public const double MaxPh = 10.0;
    public const int MaxPlotLabelLength = 100;

    /// <summary>Resolves the soil input mode, inferring it from the values present when not given.</summary>
    public static SoilInputMode ResolveMode(SoilInputDto soil) =>
        soil.Mode ?? (soil.NitrogenTotalPercent is not null || soil.PhosphorusBray1MgPerKg is not null || soil.PotassiumCmolPerKg is not null
            ? SoilInputMode.SoilTest
            : SoilInputMode.Simple);

    /// <param name="catalogue">When given, product ids are also checked for existence.</param>
    public static Dictionary<string, string[]> Validate(
        FertilizerCalculationRequest? request,
        IReadOnlyCollection<FertilizerProduct>? catalogue,
        double maxAreaHectares)
    {
        var errors = new Dictionary<string, List<string>>();
        void Add(string field, string message)
        {
            if (!errors.TryGetValue(field, out var list))
            {
                errors[field] = list = [];
            }

            list.Add(message);
        }

        if (request is null)
        {
            Add("body", "A request body is required.");
            return Freeze(errors);
        }

        if (string.IsNullOrWhiteSpace(request.CropId))
        {
            Add("cropId", "cropId is required. See GET /fertilizer/crops.");
        }

        ValidateArea(request, maxAreaHectares, Add);
        ValidateSoil(request.Soil, Add);

        if (request.TargetYieldTPerHa is { } targetYield && (targetYield <= 0 || !double.IsFinite(targetYield)))
        {
            Add("targetYieldTPerHa", "targetYieldTPerHa must be greater than 0 when given.");
        }

        var knownKeys = catalogue?.Select(product => product.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (request.FertilizerProductIds is { } productIds)
        {
            for (var i = 0; i < productIds.Count; i++)
            {
                var id = productIds[i];
                if (string.IsNullOrWhiteSpace(id))
                {
                    Add($"fertilizerProductIds[{i}]", "Product id cannot be empty.");
                }
                else if (knownKeys is not null && !knownKeys.Contains(id.Trim()))
                {
                    Add($"fertilizerProductIds[{i}]", $"Unknown fertilizer product '{id}'. See GET /fertilizer/products.");
                }
            }
        }

        if (request.CustomPrices is { } prices)
        {
            for (var i = 0; i < prices.Count; i++)
            {
                var price = prices[i];
                if (price is null || string.IsNullOrWhiteSpace(price.ProductId))
                {
                    Add($"customPrices[{i}].productId", "productId is required.");
                }
                else if (knownKeys is not null && !knownKeys.Contains(price.ProductId.Trim()))
                {
                    Add($"customPrices[{i}].productId", $"Unknown fertilizer product '{price.ProductId}'.");
                }

                if (price is not null && price.PricePerBag < 0)
                {
                    Add($"customPrices[{i}].pricePerBag", "pricePerBag cannot be negative.");
                }
            }
        }

        if (request.PlotLabel is { Length: > MaxPlotLabelLength })
        {
            Add("plotLabel", $"plotLabel cannot be longer than {MaxPlotLabelLength} characters.");
        }

        return Freeze(errors);
    }

    public static ValidationException ToException(IReadOnlyDictionary<string, string[]> errors) =>
        new($"Validation failed: {string.Join(" ", errors.Values.SelectMany(messages => messages))}", errors);

    private static void ValidateArea(FertilizerCalculationRequest request, double maxAreaHectares, Action<string, string> add)
    {
        if (!Enum.IsDefined(request.AreaUnit))
        {
            add("areaUnit", "areaUnit must be one of: hectare, acre, squareMeter, plotDimensions.");
            return;
        }

        if (request.AreaUnit == AreaUnit.PlotDimensions)
        {
            if (request.LengthM is not { } length || length <= 0 || !double.IsFinite(length))
            {
                add("lengthM", "lengthM must be greater than 0 when areaUnit is plotDimensions.");
            }

            if (request.WidthM is not { } width || width <= 0 || !double.IsFinite(width))
            {
                add("widthM", "widthM must be greater than 0 when areaUnit is plotDimensions.");
            }
        }
        else if (request.Area is not { } area || area <= 0 || !double.IsFinite(area))
        {
            add("area", "area must be greater than 0.");
        }

        var hectares = AreaConverter.ToHectares(request.Area, request.AreaUnit, request.LengthM, request.WidthM);
        if (hectares > maxAreaHectares)
        {
            add(request.AreaUnit == AreaUnit.PlotDimensions ? "lengthM" : "area",
                $"Area is {hectares:0.##} ha; the calculator accepts up to {maxAreaHectares:0.##} ha.");
        }
    }

    private static void ValidateSoil(SoilInputDto? soil, Action<string, string> add)
    {
        if (soil is null)
        {
            add("soil", "soil is required: give soil.fertility (low/medium/high) or soil-test values.");
            return;
        }

        if (soil.Mode is { } mode && !Enum.IsDefined(mode))
        {
            add("soil.mode", "soil.mode must be simple or soilTest.");
            return;
        }

        if (soil.SoilType is { } soilType && !Enum.IsDefined(soilType))
        {
            add("soil.soilType", "soil.soilType must be sandy, loamy or clay.");
        }

        if (soil.Ph is { } ph && (ph < MinPh || ph > MaxPh || !double.IsFinite(ph)))
        {
            add("soil.ph", $"soil.ph must be between {MinPh} and {MaxPh}.");
        }

        if (ResolveMode(soil) == SoilInputMode.Simple)
        {
            if (soil.Fertility is not { } fertility || !Enum.IsDefined(fertility))
            {
                add("soil.fertility", "soil.fertility (low, medium or high) is required in simple mode.");
            }

            return;
        }

        CheckSoilTestValue(soil.NitrogenTotalPercent, "soil.nitrogenTotalPercent", "total N (%)", add);
        CheckSoilTestValue(soil.PhosphorusBray1MgPerKg, "soil.phosphorusBray1MgPerKg", "Bray-1 P (mg/kg)", add);
        CheckSoilTestValue(soil.PotassiumCmolPerKg, "soil.potassiumCmolPerKg", "exchangeable K (cmol/kg)", add);
    }

    private static void CheckSoilTestValue(double? value, string field, string label, Action<string, string> add)
    {
        if (value is null)
        {
            add(field, $"{field} ({label}) is required in soilTest mode.");
        }
        else if (value < 0 || !double.IsFinite(value.Value))
        {
            add(field, $"{field} cannot be negative.");
        }
    }

    private static Dictionary<string, string[]> Freeze(Dictionary<string, List<string>> errors) =>
        errors.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray());
}
