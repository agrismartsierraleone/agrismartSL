namespace AgrismartAPI.Domain.Fertilizer;

public enum FertilizerProductType
{
    /// <summary>Carries two or more of N, P2O5 and K2O, e.g. NPK 15-15-15 or DAP.</summary>
    Compound,

    /// <summary>Carries one primary nutrient, e.g. Urea (N), TSP (P2O5), MOP (K2O).</summary>
    Straight,

    /// <summary>Manures and composts — low, variable analysis and slow release.</summary>
    Organic
}
