namespace AgrismartAPI.Application.Fertilizer;

public static class AreaConverter
{
    public const double SquareMetersPerHectare = 10_000;
    public const double HectaresPerAcre = 0.4047;

    /// <summary>Converts a validated area input to hectares.</summary>
    public static double ToHectares(double? area, AreaUnit unit, double? lengthM, double? widthM) => unit switch
    {
        AreaUnit.Hectare => area ?? 0,
        AreaUnit.Acre => (area ?? 0) * HectaresPerAcre,
        AreaUnit.SquareMeter => (area ?? 0) / SquareMetersPerHectare,
        AreaUnit.PlotDimensions => (lengthM ?? 0) * (widthM ?? 0) / SquareMetersPerHectare,
        _ => throw new ArgumentOutOfRangeException(nameof(unit), unit, "Unknown area unit.")
    };
}
