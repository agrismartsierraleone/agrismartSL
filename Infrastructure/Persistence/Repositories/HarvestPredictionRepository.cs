using AgrismartAPI.Domain.Harvest;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class HarvestPredictionRepository(AppDbContext dbContext) : IHarvestPredictionRepository
{
    public async Task<IReadOnlyList<HarvestPrediction>> GetHistoryForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await dbContext.HarvestPredictions
            .Where(prediction => prediction.UserId == userId)
            .OrderByDescending(prediction => prediction.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(HarvestPrediction prediction, CancellationToken cancellationToken = default)
    {
        dbContext.HarvestPredictions.Add(prediction);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
