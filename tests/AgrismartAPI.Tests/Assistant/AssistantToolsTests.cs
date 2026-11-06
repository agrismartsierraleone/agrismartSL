using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Assistant;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Domain.Harvest;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Persistence;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace AgrismartAPI.Tests.Assistant;

/// <summary>
/// AssistantTools against the real services/repositories (EF InMemory, synthetic weather). No AI is
/// involved: the tools are called directly, as the function-invocation layer would call them.
/// </summary>
public class AssistantToolsTests(AssistantApiFactory factory) : IClassFixture<AssistantApiFactory>
{
    // Freetown area.
    private const double Lat = 8.48;
    private const double Lon = -13.23;

    private sealed class FakeCurrentUser(Guid userId) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public Guid UserId => userId;
        public string Email => "farmer@test.local";
        public UserRole Role => UserRole.Buyer;
    }

    private async Task<(Guid FarmerId, Guid OtherFarmerId)> SeedTwoFarmersAsync()
    {
        var (farmerId, _) = await factory.CreateUserAsync();
        var (otherId, _) = await factory.CreateUserAsync();

        var farm = Farm.Create(farmerId, "River farm", "Bo", Lat, Lon, 2.0);
        farm.AddCrop("maize", DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40), 0.5);
        var otherFarm = Farm.Create(otherId, "Other farm", "Kenema", 7.9, -11.2, 5.0);
        otherFarm.AddCrop("cassava", null, 3.0);

        await factory.SeedAsync(
            farm,
            otherFarm,
            FarmRecord.Create(farmerId, farm.Id, FarmRecordType.Yield, "maize", new DateOnly(2025, 11, 1), 900, "kg", "Good season"),
            FarmRecord.Create(otherId, otherFarm.Id, FarmRecordType.Problem, "cassava", new DateOnly(2026, 3, 1), null, null, "Other farmer's secret"));

        return (farmerId, otherId);
    }

    private static AssistantTools CreateTools(IServiceScope scope, Guid farmerId) =>
        ActivatorUtilities.CreateInstance<AssistantTools>(scope.ServiceProvider, new FakeCurrentUser(farmerId));

    [Fact]
    public void No_tool_lets_the_model_choose_whose_data_to_read()
    {
        using var scope = factory.Services.CreateScope();
        var tools = CreateTools(scope, Guid.NewGuid()).CreateTools().Cast<AIFunction>().ToList();

        Assert.Equal(
            ["get_farm_profile", "get_farm_records", "get_weather_forecast", "calculate_fertilizer", "predict_harvest", "search_products", "get_my_orders", "get_my_seller_earnings", "estimate_delivery_cost", "search_jobs"],
            tools.Select(t => t.Name));
        Assert.All(tools, tool =>
        {
            var schema = tool.JsonSchema.ToString().ToLowerInvariant();
            Assert.DoesNotContain("userid", schema);
            Assert.DoesNotContain("farmerid", schema);
        });
    }

    [Fact]
    public async Task Farm_profile_returns_only_the_current_farmers_farms()
    {
        var (farmerId, _) = await SeedTwoFarmersAsync();
        using var scope = factory.Services.CreateScope();

        var profile = await CreateTools(scope, farmerId).GetFarmProfileAsync();

        var farm = Assert.Single(profile.Farms);
        Assert.Equal("River farm", farm.Name);
        Assert.Equal("Bo", farm.District);
        Assert.Equal("maize", Assert.Single(farm.Crops).CropId);
        Assert.Null(profile.Note);
    }

    [Fact]
    public async Task Farm_profile_for_a_new_farmer_says_to_ask_instead_of_inventing()
    {
        var (farmerId, _) = await factory.CreateUserAsync();
        using var scope = factory.Services.CreateScope();

        var profile = await CreateTools(scope, farmerId).GetFarmProfileAsync();

        Assert.Empty(profile.Farms);
        Assert.Contains("Ask the farmer", profile.Note);
    }

    [Fact]
    public async Task Farm_records_exclude_other_farmers()
    {
        var (farmerId, _) = await SeedTwoFarmersAsync();
        using var scope = factory.Services.CreateScope();

        var records = await CreateTools(scope, farmerId).GetFarmRecordsAsync();

        var record = Assert.Single(records.Records);
        Assert.Equal("Yield", record.Type);
        Assert.Equal(900, record.Quantity);
    }

    [Fact]
    public async Task Calculate_fertilizer_uses_the_existing_calculator_and_the_saved_plot_size()
    {
        var (farmerId, _) = await SeedTwoFarmersAsync();
        using var scope = factory.Services.CreateScope();

        var result = await CreateTools(scope, farmerId).CalculateFertilizerAsync("maize", "low");

        var plan = Assert.IsType<FertilizerCalculationResponseDto>(result);
        var expected = await scope.ServiceProvider.GetRequiredService<FertilizerAppService>().CalculateAsync(
            new FertilizerCalculationRequest("maize", 0.5, AreaUnit.Hectare, Soil: new SoilInputDto(SoilInputMode.Simple, SoilFertility.Low)));
        Assert.Equal(0.5, plan.AreaHectares, 6);
        Assert.Equal(expected.Products.Select(p => (p.ProductId, p.QuantityKg)), plan.Products.Select(p => (p.ProductId, p.QuantityKg)));
    }

    [Fact]
    public async Task Calculate_fertilizer_returns_valid_crop_ids_for_an_unknown_crop()
    {
        var (farmerId, _) = await SeedTwoFarmersAsync();
        using var scope = factory.Services.CreateScope();

        var result = await CreateTools(scope, farmerId).CalculateFertilizerAsync("banana-tree", "medium", areaHectares: 1);

        var error = Assert.IsType<ToolError>(result);
        Assert.Contains("maize", error.ValidCropIds!);
    }

    [Fact]
    public async Task Calculate_fertilizer_asks_for_missing_inputs_instead_of_guessing()
    {
        var (farmerId, _) = await factory.CreateUserAsync();
        using var scope = factory.Services.CreateScope();
        var tools = CreateTools(scope, farmerId);

        Assert.Contains("soilFertility", Assert.IsType<ToolError>(await tools.CalculateFertilizerAsync("maize", "very good", 1)).Error);
        Assert.Contains("field size", Assert.IsType<ToolError>(await tools.CalculateFertilizerAsync("maize", "medium")).Error);
    }

    [Fact]
    public async Task Predict_harvest_uses_farm_location_and_planting_date_without_writing_history()
    {
        var (farmerId, _) = await SeedTwoFarmersAsync();
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var historyBefore = dbContext.HarvestPredictions.Count();

        var result = await CreateTools(scope, farmerId).PredictHarvestAsync("maize");

        var prediction = Assert.IsType<HarvestPredictionToolResult>(result);
        Assert.Equal(Lat, prediction.Latitude);
        Assert.Equal(Lon, prediction.Longitude);
        Assert.Equal(DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-40), prediction.PlantingDate);

        var crop = (await scope.ServiceProvider.GetRequiredService<ICropRepository>().GetByKeyAsync("maize"))!;
        var expected = await scope.ServiceProvider.GetRequiredService<IHarvestPredictor>()
            .PredictAsync(crop, Lat, Lon, prediction.PlantingDate, null);
        Assert.Equal(expected.Harvest.Expected, prediction.Harvest.Expected);
        Assert.Equal(historyBefore, dbContext.HarvestPredictions.Count());
    }

    [Fact]
    public async Task Predict_harvest_rejects_bad_input_with_a_helpful_error()
    {
        var (farmerId, _) = await factory.CreateUserAsync();
        using var scope = factory.Services.CreateScope();
        var tools = CreateTools(scope, farmerId);

        Assert.Contains("rice-lowland", Assert.IsType<ToolError>(await tools.PredictHarvestAsync("rice")).ValidCropIds!);
        Assert.Contains("yyyy-MM-dd", Assert.IsType<ToolError>(await tools.PredictHarvestAsync("maize", "12/05/2026")).Error);
        Assert.Contains("location", Assert.IsType<ToolError>(await tools.PredictHarvestAsync("maize", "2026-06-01")).Error);
    }

    [Fact]
    public async Task Weather_forecast_uses_the_farm_location()
    {
        var (farmerId, _) = await SeedTwoFarmersAsync();
        using var scope = factory.Services.CreateScope();

        var result = await CreateTools(scope, farmerId).GetWeatherForecastAsync(days: 5);

        var forecast = Assert.IsType<WeatherForecastResult>(result);
        Assert.Equal(Lat, forecast.Latitude);
        Assert.Equal("farm 'River farm'", forecast.LocationSource);
        Assert.NotEmpty(forecast.Days);
        Assert.Contains("No rainfall", forecast.Note);
    }

    [Fact]
    public async Task Weather_forecast_without_any_location_asks_the_farmer()
    {
        var (farmerId, _) = await factory.CreateUserAsync();
        using var scope = factory.Services.CreateScope();

        var result = await CreateTools(scope, farmerId).GetWeatherForecastAsync();

        Assert.IsType<ToolError>(result);
    }
}
