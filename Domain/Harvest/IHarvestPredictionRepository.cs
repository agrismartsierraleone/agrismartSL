namespace AgrismartAPI.Domain.Harvest;

public interface IHarvestPredictionRepository
{
    Task<IReadOnlyList<HarvestPrediction>> GetHistoryForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    Task AddAsync(HarvestPrediction prediction, CancellationToken cancellationToken = default);
}
