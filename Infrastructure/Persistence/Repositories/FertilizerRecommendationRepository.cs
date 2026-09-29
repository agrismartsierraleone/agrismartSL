using AgrismartAPI.Domain.Fertilizer;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Persistence.Repositories;

public class FertilizerRecommendationRepository(AppDbContext dbContext) : IFertilizerRecommendationRepository
{
    public Task<FertilizerRecommendation?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return dbContext.FertilizerRecommendations.SingleOrDefaultAsync(recommendation => recommendation.Id == id, cancellationToken);
    }

    public async Task<(IReadOnlyList<FertilizerRecommendation> Items, int TotalCount)> SearchAsync(
        Guid? userId,
        string? cropId,
        string? plotLabel,
        int skip,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.FertilizerRecommendations.AsNoTracking();

        if (userId is not null)
        {
            query = query.Where(recommendation => recommendation.UserId == userId);
        }

        if (cropId is not null)
        {
            query = query.Where(recommendation => recommendation.CropId == cropId);
        }

        if (plotLabel is not null)
        {
            var normalized = plotLabel.ToLower();
            query = query.Where(recommendation => recommendation.PlotLabel != null && recommendation.PlotLabel.ToLower() == normalized);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(recommendation => recommendation.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task AddAsync(FertilizerRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        dbContext.FertilizerRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(FertilizerRecommendation recommendation, CancellationToken cancellationToken = default)
    {
        dbContext.FertilizerRecommendations.Remove(recommendation);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
