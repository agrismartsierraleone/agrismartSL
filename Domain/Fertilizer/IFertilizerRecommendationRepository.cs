namespace AgrismartAPI.Domain.Fertilizer;

public interface IFertilizerRecommendationRepository
{
    Task<FertilizerRecommendation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Newest first. Null filters are ignored; <paramref name="plotLabel"/> matches case-insensitively.</summary>
    Task<(IReadOnlyList<FertilizerRecommendation> Items, int TotalCount)> SearchAsync(
        Guid? userId,
        string? cropId,
        string? plotLabel,
        int skip,
        int take,
        CancellationToken cancellationToken = default);

    Task AddAsync(FertilizerRecommendation recommendation, CancellationToken cancellationToken = default);

    Task DeleteAsync(FertilizerRecommendation recommendation, CancellationToken cancellationToken = default);
}
