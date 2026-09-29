using AgrismartAPI.Domain.Fertilizer;

namespace AgrismartAPI.Application.Fertilizer;

internal enum Nutrient
{
    N,
    P,
    K
}

/// <summary>Kilograms of N, P2O5 and K2O, with the arithmetic the calculator needs.</summary>
internal readonly record struct Nutrients(double N, double P, double K)
{
    public static readonly Nutrients Zero = new(0, 0, 0);

    public double Total => N + P + K;

    public double this[Nutrient nutrient] => nutrient switch
    {
        Nutrient.N => N,
        Nutrient.P => P,
        _ => K
    };

    public static Nutrients operator +(Nutrients a, Nutrients b) => new(a.N + b.N, a.P + b.P, a.K + b.K);

    public static Nutrients operator -(Nutrients a, Nutrients b) => new(a.N - b.N, a.P - b.P, a.K - b.K);

    public static Nutrients operator *(Nutrients a, double factor) => new(a.N * factor, a.P * factor, a.K * factor);

    public Nutrients ClampAtZero() => new(Math.Max(0, N), Math.Max(0, P), Math.Max(0, K));

    /// <summary>Nutrients in <paramref name="kg"/> of <paramref name="product"/>: kg × (% ÷ 100).</summary>
    public static Nutrients SuppliedBy(FertilizerProduct product, double kg) =>
        new(kg * product.NitrogenPercent / 100, kg * product.PhosphatePercent / 100, kg * product.PotashPercent / 100);

    public static double Content(FertilizerProduct product, Nutrient nutrient) => nutrient switch
    {
        Nutrient.N => product.NitrogenPercent,
        Nutrient.P => product.PhosphatePercent,
        _ => product.PotashPercent
    };

    public NutrientAmountsDto ToDto() => new(Round1(N), Round1(P), Round1(K));

    public static double Round1(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);
}
