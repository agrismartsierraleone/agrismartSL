namespace AgrismartAPI.Domain.Assistant;

public interface IFarmRepository
{
    /// <summary>The farmer's farms with their crops, oldest first.</summary>
    Task<IReadOnlyList<Farm>> GetFarmsForUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<FarmRecord>> GetRecordsForUserAsync(Guid userId, int take, CancellationToken cancellationToken = default);
}
