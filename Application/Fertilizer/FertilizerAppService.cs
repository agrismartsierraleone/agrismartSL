using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Users;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Fertilizer;

public partial class FertilizerAppService(
    IFertilizerCropRepository cropRepository,
    IFertilizerProductRepository productRepository,
    IFertilizerRecommendationRepository recommendationRepository,
    IFertilizerCalculatorService calculator,
    ICurrentUserService currentUser,
    IOptions<FertilizerCalculatorOptions> options)
{
    public const int MaxPageSize = 100;

    /// <summary>Same shape the API returns, so stored snapshots deserialize back into the DTOs unchanged.</summary>
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex KeyPattern();

    // ---------- Crops ----------

    public async Task<IReadOnlyList<FertilizerCropDto>> GetCropsAsync(CancellationToken cancellationToken = default)
    {
        var crops = await cropRepository.GetAllAsync(cancellationToken);
        return crops.Select(FertilizerMapper.ToDto).ToList();
    }

    public async Task<FertilizerCropDto> GetCropAsync(string cropId, CancellationToken cancellationToken = default)
    {
        var crop = await cropRepository.GetByKeyAsync(cropId, cancellationToken)
                   ?? throw new NotFoundException($"No fertilizer crop profile with id '{cropId}'.");
        return FertilizerMapper.ToDto(crop);
    }

    public async Task<FertilizerCropDto> CreateCropAsync(FertilizerCropRequest request, CancellationToken cancellationToken = default)
    {
        var key = ValidateNewKey(request.Id);
        if (await cropRepository.GetByKeyAsync(key, cancellationToken) is not null)
        {
            throw new ConflictException($"A fertilizer crop profile with id '{key}' already exists.");
        }

        var crop = ToDomainArguments(() => FertilizerCropProfile.Create(
            key, request.Name, request.LocalName, request.NitrogenKgPerHa, request.PhosphateKgPerHa, request.PotashKgPerHa,
            request.ReferenceYieldTPerHa, request.MaxRealisticYieldTPerHa, request.YieldBasis ?? "", request.PlantsPerHectare,
            request.IsPerennial, request.Notes, ToSchedule(request.Schedule)));

        await cropRepository.AddAsync(crop, cancellationToken);
        return FertilizerMapper.ToDto(crop);
    }

    public async Task<FertilizerCropDto> UpdateCropAsync(string cropId, FertilizerCropRequest request, CancellationToken cancellationToken = default)
    {
        var crop = await cropRepository.GetByKeyAsync(cropId, cancellationToken)
                   ?? throw new NotFoundException($"No fertilizer crop profile with id '{cropId}'.");

        ToDomainArguments(() => crop.Update(
            request.Name, request.LocalName, request.NitrogenKgPerHa, request.PhosphateKgPerHa, request.PotashKgPerHa,
            request.ReferenceYieldTPerHa, request.MaxRealisticYieldTPerHa, request.YieldBasis ?? "", request.PlantsPerHectare,
            request.IsPerennial, request.Notes, ToSchedule(request.Schedule)));

        await cropRepository.UpdateAsync(crop, cancellationToken);
        return FertilizerMapper.ToDto(crop);
    }

    // ---------- Products ----------

    public async Task<IReadOnlyList<FertilizerProductDto>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        var products = await productRepository.GetAllAsync(cancellationToken);
        return products.Select(product => FertilizerMapper.ToDto(product, options.Value.Currency)).ToList();
    }

    public async Task<FertilizerProductDto> GetProductAsync(string productId, CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByKeyAsync(productId, cancellationToken)
                      ?? throw new NotFoundException($"No fertilizer product with id '{productId}'.");
        return FertilizerMapper.ToDto(product, options.Value.Currency);
    }

    public async Task<FertilizerProductDto> CreateProductAsync(FertilizerProductRequest request, CancellationToken cancellationToken = default)
    {
        var key = ValidateNewKey(request.Id);
        if (await productRepository.GetByKeyAsync(key, cancellationToken) is not null)
        {
            throw new ConflictException($"A fertilizer product with id '{key}' already exists.");
        }

        var product = ToDomainArguments(() => FertilizerProduct.Create(
            key, request.Name, RequireDefined(request.Type), request.NitrogenPercent, request.PhosphatePercent, request.PotashPercent,
            request.SulphurPercent, request.ZincPercent, request.BagSizeKg ?? FertilizerProduct.DefaultBagSizeKg,
            request.PricePerBag, request.Notes));

        await productRepository.AddAsync(product, cancellationToken);
        return FertilizerMapper.ToDto(product, options.Value.Currency);
    }

    public async Task<FertilizerProductDto> UpdateProductAsync(string productId, FertilizerProductRequest request, CancellationToken cancellationToken = default)
    {
        var product = await productRepository.GetByKeyAsync(productId, cancellationToken)
                      ?? throw new NotFoundException($"No fertilizer product with id '{productId}'.");

        ToDomainArguments(() => product.Update(
            request.Name, RequireDefined(request.Type), request.NitrogenPercent, request.PhosphatePercent, request.PotashPercent,
            request.SulphurPercent, request.ZincPercent, request.BagSizeKg ?? FertilizerProduct.DefaultBagSizeKg,
            request.PricePerBag, request.Notes));

        await productRepository.UpdateAsync(product, cancellationToken);
        return FertilizerMapper.ToDto(product, options.Value.Currency);
    }

    // ---------- Calculation ----------

    public async Task<FertilizerCalculationResponseDto> CalculateAsync(
        FertilizerCalculationRequest request, CancellationToken cancellationToken = default)
    {
        var (_, result) = await CalculateCoreAsync(request, cancellationToken);
        return result;
    }

    public async Task<FertilizerRecommendationDto> SaveRecommendationAsync(
        FertilizerCalculationRequest request, CancellationToken cancellationToken = default)
    {
        var (crop, result) = await CalculateCoreAsync(request, cancellationToken);

        var recommendation = FertilizerRecommendation.Create(
            currentUser.UserId,
            crop.Key,
            crop.Name,
            request.PlotLabel,
            result.AreaHectares,
            result.TotalEstimatedCost,
            result.Currency,
            JsonSerializer.Serialize(request, SnapshotJsonOptions),
            JsonSerializer.Serialize(result, SnapshotJsonOptions));

        await recommendationRepository.AddAsync(recommendation, cancellationToken);
        return ToDto(recommendation);
    }

    // ---------- History ----------

    public async Task<PagedResult<FertilizerRecommendationSummaryDto>> GetRecommendationsAsync(
        FertilizerRecommendationQuery query, CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string[]>();
        if (query.Page < 1)
        {
            errors["page"] = ["page must be 1 or greater."];
        }

        if (query.PageSize is < 1 or > MaxPageSize)
        {
            errors["pageSize"] = [$"pageSize must be between 1 and {MaxPageSize}."];
        }

        if (errors.Count > 0)
        {
            throw FertilizerRequestValidator.ToException(errors);
        }

        Guid? userFilter;
        if (IsAdmin)
        {
            userFilter = query.UserId;
        }
        else if (query.UserId is { } requested && requested != currentUser.UserId)
        {
            throw new ForbiddenException("You can only view your own fertilizer recommendations.");
        }
        else
        {
            userFilter = currentUser.UserId;
        }

        var (items, totalCount) = await recommendationRepository.SearchAsync(
            userFilter,
            string.IsNullOrWhiteSpace(query.CropId) ? null : query.CropId.Trim().ToLowerInvariant(),
            string.IsNullOrWhiteSpace(query.PlotLabel) ? null : query.PlotLabel.Trim(),
            (query.Page - 1) * query.PageSize,
            query.PageSize,
            cancellationToken);

        return new PagedResult<FertilizerRecommendationSummaryDto>(
            items.Select(FertilizerMapper.ToSummaryDto).ToList(), query.Page, query.PageSize, totalCount);
    }

    public async Task<FertilizerRecommendationDto> GetRecommendationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, cancellationToken);
        return ToDto(recommendation);
    }

    public async Task DeleteRecommendationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var recommendation = await GetOwnedRecommendationAsync(id, cancellationToken);
        await recommendationRepository.DeleteAsync(recommendation, cancellationToken);
    }

    // ---------- Helpers ----------

    private bool IsAdmin => currentUser.Role == UserRole.Admin;

    private async Task<(FertilizerCropProfile Crop, FertilizerCalculationResponseDto Result)> CalculateCoreAsync(
        FertilizerCalculationRequest request, CancellationToken cancellationToken)
    {
        var catalogue = await productRepository.GetAllAsync(cancellationToken);
        var errors = FertilizerRequestValidator.Validate(request, catalogue, options.Value.MaxAreaHectares);

        FertilizerCropProfile? crop = null;
        if (!string.IsNullOrWhiteSpace(request?.CropId))
        {
            crop = await cropRepository.GetByKeyAsync(request.CropId, cancellationToken);
            if (crop is null)
            {
                errors["cropId"] = [$"Unknown crop '{request.CropId}'. See GET /fertilizer/crops."];
            }
        }

        if (errors.Count > 0)
        {
            throw FertilizerRequestValidator.ToException(errors);
        }

        return (crop!, calculator.Calculate(request!, crop!, catalogue));
    }

    /// <summary>Owners see their own records; admins see all. Anyone else gets 404 so ids are not leaked.</summary>
    private async Task<FertilizerRecommendation> GetOwnedRecommendationAsync(Guid id, CancellationToken cancellationToken)
    {
        var recommendation = await recommendationRepository.GetByIdAsync(id, cancellationToken);
        if (recommendation is null || (!IsAdmin && recommendation.UserId != currentUser.UserId))
        {
            throw new NotFoundException($"Fertilizer recommendation '{id}' was not found.");
        }

        return recommendation;
    }

    private static FertilizerRecommendationDto ToDto(FertilizerRecommendation recommendation) => new(
        recommendation.Id,
        recommendation.UserId,
        recommendation.CropId,
        recommendation.CropName,
        recommendation.PlotLabel ?? "",
        recommendation.AreaHectares,
        recommendation.CreatedAt,
        JsonSerializer.Deserialize<FertilizerCalculationRequest>(recommendation.InputsJson, SnapshotJsonOptions)!,
        JsonSerializer.Deserialize<FertilizerCalculationResponseDto>(recommendation.ResultJson, SnapshotJsonOptions)!);

    private static string ValidateNewKey(string? id)
    {
        var key = id?.Trim().ToLowerInvariant() ?? "";
        if (!KeyPattern().IsMatch(key) || key.Length > 50)
        {
            throw FertilizerRequestValidator.ToException(new Dictionary<string, string[]>
            {
                ["id"] = ["id is required: up to 50 lowercase letters, digits and hyphens, e.g. \"npk-17-17-17\"."]
            });
        }

        return key;
    }

    private static FertilizerProductType RequireDefined(FertilizerProductType type) =>
        Enum.IsDefined(type)
            ? type
            : throw FertilizerRequestValidator.ToException(new Dictionary<string, string[]>
            {
                ["type"] = ["type must be compound, straight or organic."]
            });

    private static IReadOnlyList<FertilizerSplit> ToSchedule(IReadOnlyList<FertilizerSplitDto>? schedule) =>
        (schedule ?? []).Select(FertilizerMapper.ToDomain).ToList();

    /// <summary>Domain guard clauses throw ArgumentException; surface them as 400s rather than 500s.</summary>
    private static T ToDomainArguments<T>(Func<T> action)
    {
        try
        {
            return action();
        }
        catch (ArgumentException exception)
        {
            var message = exception.ParamName is null
                ? exception.Message
                : exception.Message.Replace($" (Parameter '{exception.ParamName}')", "");
            throw new ValidationException(message);
        }
    }

    private static void ToDomainArguments(Action action) => ToDomainArguments(() =>
    {
        action();
        return true;
    });
}
