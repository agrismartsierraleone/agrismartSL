using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Controllers;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Infrastructure.Persistence.Seed;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace AgrismartAPI.OpenApi;

/// <summary>
/// Adds example requests and responses to every FertilizerController operation. The calculation
/// examples are produced by running the real calculator on the seed reference data, so the docs
/// never drift from actual behaviour.
/// </summary>
internal sealed class FertilizerExamplesTransformer(IOptions<FertilizerCalculatorOptions> options) : IOpenApiOperationTransformer
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly Lazy<Examples> examples = new(() => Examples.Build(options));

    public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
    {
        if (context.Description.ActionDescriptor is not ControllerActionDescriptor action ||
            action.ControllerTypeInfo.AsType() != typeof(FertilizerController))
        {
            return Task.CompletedTask;
        }

        var e = examples.Value;
        switch (action.ActionName)
        {
            case nameof(FertilizerController.GetCrops):
                SetResponse(operation, "200", new[] { e.MaizeCrop });
                break;
            case nameof(FertilizerController.GetCrop):
                SetResponse(operation, "200", e.MaizeCrop);
                SetResponse(operation, "404", new { message = "No fertilizer crop profile with id 'wheat'." });
                break;
            case nameof(FertilizerController.CreateCrop):
            case nameof(FertilizerController.UpdateCrop):
                SetRequest(operation, e.CropRequest);
                SetResponse(operation, action.ActionName == nameof(FertilizerController.CreateCrop) ? "201" : "200", e.MaizeCrop);
                SetResponse(operation, "400", new { message = "Schedule N percentages must add up to 100 (got 90)." });
                break;
            case nameof(FertilizerController.GetProducts):
                SetResponse(operation, "200", e.Products);
                break;
            case nameof(FertilizerController.GetProduct):
                SetResponse(operation, "200", e.Products[0]);
                SetResponse(operation, "404", new { message = "No fertilizer product with id 'npk-17-17-17'." });
                break;
            case nameof(FertilizerController.CreateProduct):
            case nameof(FertilizerController.UpdateProduct):
                SetRequest(operation, e.ProductRequest);
                SetResponse(operation, action.ActionName == nameof(FertilizerController.CreateProduct) ? "201" : "200", e.Products[0]);
                break;
            case nameof(FertilizerController.Calculate):
                SetRequest(operation, e.CalculationRequest);
                SetResponse(operation, "200", e.CalculationResult);
                SetResponse(operation, "400", e.ValidationError);
                break;
            case nameof(FertilizerController.SaveRecommendation):
                SetRequest(operation, e.CalculationRequest);
                SetResponse(operation, "201", e.Recommendation);
                SetResponse(operation, "400", e.ValidationError);
                break;
            case nameof(FertilizerController.GetRecommendations):
                SetResponse(operation, "200", e.RecommendationPage);
                break;
            case nameof(FertilizerController.GetRecommendation):
                SetResponse(operation, "200", e.Recommendation);
                SetResponse(operation, "404", new { message = "Fertilizer recommendation '3f2b8c1e-4d5a-4b7e-9c61-2a8e7d4f1b90' was not found." });
                break;
        }

        return Task.CompletedTask;
    }

    private static JsonNode? ToNode(object value) => JsonSerializer.SerializeToNode(value, value.GetType(), JsonOptions);

    private static void SetRequest(OpenApiOperation operation, object example)
    {
        if (operation.RequestBody?.Content is { } content)
        {
            foreach (var mediaType in content.Values)
            {
                mediaType.Example = ToNode(example);
            }
        }
    }

    private static void SetResponse(OpenApiOperation operation, string statusCode, object example)
    {
        if (operation.Responses?.TryGetValue(statusCode, out var response) != true || response is not OpenApiResponse concrete)
        {
            return;
        }

        concrete.Content ??= new Dictionary<string, OpenApiMediaType>();
        if (!concrete.Content.TryGetValue("application/json", out var mediaType))
        {
            mediaType = new OpenApiMediaType();
            concrete.Content["application/json"] = mediaType;
        }

        mediaType.Example = ToNode(example);
    }

    private sealed record Examples(
        FertilizerCropDto MaizeCrop,
        FertilizerCropRequest CropRequest,
        IReadOnlyList<FertilizerProductDto> Products,
        FertilizerProductRequest ProductRequest,
        FertilizerCalculationRequest CalculationRequest,
        FertilizerCalculationResponseDto CalculationResult,
        FertilizerRecommendationDto Recommendation,
        PagedResult<FertilizerRecommendationSummaryDto> RecommendationPage,
        object ValidationError)
    {
        public static Examples Build(IOptions<FertilizerCalculatorOptions> options)
        {
            var currency = options.Value.Currency;
            var crops = FertilizerReferenceData.Crops();
            var maize = crops.First(crop => crop.Key == "maize");

            // Illustrative prices only — seed data ships without prices.
            var catalogue = FertilizerReferenceData.Products();
            var npk = catalogue.First(p => p.Key == "npk-15-15-15");
            var urea = catalogue.First(p => p.Key == "urea");
            npk.Update(npk.Name, npk.Type, npk.NitrogenPercent, npk.PhosphatePercent, npk.PotashPercent,
                npk.SulphurPercent, npk.ZincPercent, npk.BagSizeKg, 950m, npk.Notes);
            urea.Update(urea.Name, urea.Type, urea.NitrogenPercent, urea.PhosphatePercent, urea.PotashPercent,
                urea.SulphurPercent, urea.ZincPercent, urea.BagSizeKg, 900m, urea.Notes);

            var request = new FertilizerCalculationRequest(
                CropId: "maize",
                Area: 0.5,
                AreaUnit: AreaUnit.Hectare,
                Soil: new SoilInputDto(SoilInputMode.Simple, SoilFertility.Medium, SoilType.Loamy, 5.2),
                TargetYieldTPerHa: 4.0,
                FertilizerProductIds: ["npk-15-15-15", "urea"],
                PlotLabel: "Back field by the stream");

            var result = new FertilizerCalculatorService(options).Calculate(request, maize, catalogue);
            var createdAt = new DateTimeOffset(2026, 9, 29, 10, 30, 0, TimeSpan.Zero);
            var id = Guid.Parse("3f2b8c1e-4d5a-4b7e-9c61-2a8e7d4f1b90");
            var recommendation = new FertilizerRecommendationDto(
                id, Guid.Parse("9a1c6e52-0b7d-4f3a-8e25-6d4b1f0c7a38"), "maize", "Maize", request.PlotLabel!,
                result.AreaHectares, createdAt, request, result);

            return new Examples(
                FertilizerMapper.ToDto(maize),
                new FertilizerCropRequest(
                    "maize", "Maize", "Kon", 90, 45, 45, 3.5, 7.0, "dry grain", 53_333, false, null,
                    maize.Schedule.Select(FertilizerMapper.ToDto).ToList()),
                [FertilizerMapper.ToDto(npk, currency), FertilizerMapper.ToDto(urea, currency)],
                new FertilizerProductRequest("npk-17-17-17", "NPK 17-17-17", FertilizerProductType.Compound,
                    17, 17, 17, null, null, 50, 1000m, "Balanced compound."),
                request,
                result,
                recommendation,
                new PagedResult<FertilizerRecommendationSummaryDto>(
                    [new FertilizerRecommendationSummaryDto(id, "maize", "Maize", request.PlotLabel!, result.AreaHectares,
                        result.TotalEstimatedCost, result.Currency, createdAt)],
                    1, 20, 1),
                new
                {
                    message = "Validation failed: area must be greater than 0. soil.fertility (low, medium or high) is required in simple mode.",
                    errors = new Dictionary<string, string[]>
                    {
                        ["area"] = ["area must be greater than 0."],
                        ["soil.fertility"] = ["soil.fertility (low, medium or high) is required in simple mode."]
                    }
                });
        }
    }
}
