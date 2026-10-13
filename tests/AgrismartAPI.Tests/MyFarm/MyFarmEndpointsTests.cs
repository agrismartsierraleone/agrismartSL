using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Domain.Diagnoses;
using AgrismartAPI.Domain.Farms;
using AgrismartAPI.Domain.Scans;
using AgrismartAPI.Tests.Assistant;

namespace AgrismartAPI.Tests.MyFarm;

/// <summary>Runs against EF InMemory through <see cref="AssistantApiFactory"/>; never the configured database.</summary>
public class MyFarmEndpointsTests(AssistantApiFactory factory) : IClassFixture<AssistantApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private async Task<(HttpClient Client, Guid UserId)> SignedInClientAsync()
    {
        var (userId, token) = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return (client, userId);
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private static async Task<Guid> AddFarmAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/farms", new { name = "Green Valley Farm", district = "Port Loko", area = 4.5 });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await ReadJsonAsync(response))["id"]!.GetValue<Guid>();
    }

    private static async Task<JsonNode> AddRiceAsync(HttpClient client, Guid farmId, int daysAgo = 30)
    {
        var response = await client.PostAsJsonAsync($"/farms/{farmId}/crops", new
        {
            cropId = "rice-lowland",
            variety = "NERICA 14",
            plantingDate = Today.AddDays(-daysAgo).ToString("yyyy-MM-dd"),
            plotSize = 3.0
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    [Fact]
    public async Task Requires_sign_in()
    {
        var client = factory.CreateClient();
        var id = Guid.NewGuid();

        foreach (var url in new[] { $"/farms/{id}/overview", $"/farms/{id}/crops", $"/farms/{id}/activities", $"/farms/{id}/analytics", $"/farms/{id}/map" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(url)).StatusCode);
        }
    }

    [Fact]
    public async Task Options_list_crops_and_activity_types()
    {
        var (client, _) = await SignedInClientAsync();

        var options = await ReadJsonAsync(await client.GetAsync("/farms/options"));

        Assert.Contains(options["crops"]!.AsArray(), crop => crop!["id"]!.GetValue<string>() == "okra");
        Assert.Contains(options["activityTypes"]!.AsArray(), type => type!["type"]!.GetValue<string>() == "pestControl" && type["stageKey"]!.GetValue<string>() == "pestControl");
        Assert.Equal("SLE", options["currency"]!.GetValue<string>());
    }

    [Fact]
    public async Task Add_crop_returns_a_card_and_plans_its_tasks()
    {
        var (client, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);

        var crop = await AddRiceAsync(client, farmId);

        Assert.Equal("growing", crop["status"]!.GetValue<string>());
        Assert.Equal("Plot 1", crop["plotName"]!.GetValue<string>());
        Assert.Equal("NERICA 14", crop["variety"]!.GetValue<string>());
        Assert.True(crop["expectedHarvestIsEstimate"]!.GetValue<bool>());
        Assert.InRange(crop["progressPercent"]!.GetValue<int>(), 20, 30);
        Assert.Equal("Tillering", crop["growthStage"]!.GetValue<string>());
        Assert.True(crop["pendingActivityCount"]!.GetValue<int>() > 0);

        var second = await (await client.PostAsJsonAsync($"/farms/{farmId}/crops", new { cropId = "cassava", generateActivities = false })).Content.ReadFromJsonAsync<JsonNode>();
        Assert.Equal(("Plot 2", "planned"), (second!["plotName"]!.GetValue<string>(), second["status"]!.GetValue<string>()));

        var activities = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/activities?status=pending"));
        var items = activities["items"]!.AsArray();
        Assert.All(items, item => Assert.True(DateOnly.Parse(item!["dueDate"]!.GetValue<string>()) >= Today));
        Assert.All(items, item => Assert.Equal("calendar", item!["source"]!.GetValue<string>()));
        Assert.Equal("Plot 1", items[0]!["plotName"]!.GetValue<string>());
    }

    [Fact]
    public async Task Overview_has_everything_for_the_crops_tab()
    {
        var (client, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);
        var rice = await AddRiceAsync(client, farmId);

        var overview = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/overview"));

        Assert.Equal("Green Valley Farm", overview["farm"]!["name"]!.GetValue<string>());
        Assert.Equal(3.0, overview["farm"]!["cultivatedHectares"]!.GetValue<double>());
        Assert.Single(overview["crops"]!.AsArray());
        var calendar = overview["calendar"]!;
        Assert.Equal(rice["id"]!.GetValue<Guid>(), calendar["farmCropId"]!.GetValue<Guid>());
        Assert.Equal(6, calendar["stages"]!.AsArray().Count);
        Assert.Equal("fertilizing", calendar["currentStageKey"]!.GetValue<string>());
        var upcoming = overview["upcomingActivities"]!.AsArray();
        Assert.Equal(3, upcoming.Count);
        Assert.True(overview["pendingActivityCount"]!.GetValue<int>() >= 3);
        var dueDates = upcoming.Select(a => DateOnly.Parse(a!["dueDate"]!.GetValue<string>())).ToList();
        Assert.Equal(dueDates.Order(), dueDates);
        Assert.Null(overview["cropHealth"]);
    }

    [Fact]
    public async Task Activities_can_be_added_completed_reopened_and_counted()
    {
        var (client, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);
        var rice = await AddRiceAsync(client, farmId);

        var bad = await client.PostAsJsonAsync($"/farms/{farmId}/activities", new { type = "weeding", title = "", dueDate = Today, farmCropId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);
        var errors = (await ReadJsonAsync(bad))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("title") && errors.ContainsKey("farmCropId"));

        var created = await client.PostAsJsonAsync($"/farms/{farmId}/activities", new
        {
            type = "fertilizing", title = "Buy urea", dueDate = Today.AddDays(1), farmCropId = rice["id"]!.GetValue<Guid>(), cost = 150
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var activity = await ReadJsonAsync(created);
        Assert.Equal((1, "manual", "Rice (lowland)"),
            (activity["dueInDays"]!.GetValue<int>(), activity["source"]!.GetValue<string>(), activity["cropName"]!.GetValue<string>()));
        var id = activity["id"]!.GetValue<Guid>();

        var done = await ReadJsonAsync(await client.PostAsJsonAsync($"/farms/{farmId}/activities/{id}/complete", new { cost = 200 }));
        Assert.Equal(("completed", 200m), (done["status"]!.GetValue<string>(), done["cost"]!.GetValue<decimal>()));

        // Body is optional.
        var reopened = await client.PostAsync($"/farms/{farmId}/activities/{id}/reopen", null);
        Assert.Equal("pending", (await ReadJsonAsync(reopened))["status"]!.GetValue<string>());
        var completedAgain = await client.PostAsync($"/farms/{farmId}/activities/{id}/complete", null);
        Assert.Equal(HttpStatusCode.OK, completedAgain.StatusCode);

        var analytics = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/analytics"));
        Assert.Equal(1, analytics["activities"]!["completed"]!.GetValue<int>());
        Assert.Equal(200m, analytics["totalCost"]!.GetValue<decimal>());
        Assert.Equal(6, analytics["monthly"]!.AsArray().Count);
        Assert.Equal(1, analytics["crops"]!["growing"]!.GetValue<int>());
        Assert.Equal(66.7, analytics["landUsePercent"]!.GetValue<double>());

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/farms/{farmId}/activities/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/farms/{farmId}/activities/{id}/skip", null)).StatusCode);
    }

    [Fact]
    public async Task Harvest_records_the_yield_and_closes_calendar_tasks()
    {
        var (client, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);
        var rice = await AddRiceAsync(client, farmId, daysAgo: 120);
        var cropId = rice["id"]!.GetValue<Guid>();

        var harvested = await ReadJsonAsync(await client.PostAsJsonAsync($"/farms/{farmId}/crops/{cropId}/harvest", new { quantity = 1200, unit = "kg" }));

        Assert.Equal(("harvested", 100), (harvested["status"]!.GetValue<string>(), harvested["progressPercent"]!.GetValue<int>()));
        Assert.Equal(0, harvested["pendingActivityCount"]!.GetValue<int>());
        var analytics = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/analytics"));
        var yield = Assert.Single(analytics["yields"]!.AsArray())!;
        Assert.Equal((1200d, "kg"), (yield["quantity"]!.GetValue<double>(), yield["unit"]!.GetValue<string>()));
        Assert.Equal(1, analytics["crops"]!["harvested"]!.GetValue<int>());

        var calendar = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/crops/{cropId}/calendar"));
        Assert.Null(calendar["currentStageKey"]);

        var reactivated = await ReadJsonAsync(await client.PutAsJsonAsync($"/farms/{farmId}/crops/{cropId}/status", new { status = "active" }));
        Assert.Equal("growing", reactivated["status"]!.GetValue<string>());
    }

    [Fact]
    public async Task Changing_the_planting_date_replans_open_calendar_tasks()
    {
        var (client, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);
        var rice = await AddRiceAsync(client, farmId);
        var cropId = rice["id"]!.GetValue<Guid>();
        var newPlanting = Today.AddDays(10);

        var updated = await ReadJsonAsync(await client.PutAsJsonAsync($"/farms/{farmId}/crops/{cropId}", new
        {
            cropId = "rice-lowland", plotName = "Swamp plot", variety = "NERICA 14", plantingDate = newPlanting.ToString("yyyy-MM-dd"), plotSize = 3.0,
            latitude = 8.77, longitude = -12.78
        }));

        Assert.Equal(("planned", "Swamp plot"), (updated["status"]!.GetValue<string>(), updated["plotName"]!.GetValue<string>()));
        var calendar = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/crops/{cropId}/calendar"));
        var planting = calendar["activities"]!.AsArray().Single(a => a!["type"]!.GetValue<string>() == "planting")!;
        Assert.Equal(newPlanting.ToString("yyyy-MM-dd"), planting["dueDate"]!.GetValue<string>());
        Assert.Equal("landPreparation", calendar["currentStageKey"]!.GetValue<string>());
    }

    [Fact]
    public async Task Map_stores_farm_and_plot_outlines()
    {
        var (client, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);
        await AddRiceAsync(client, farmId);
        var outline = new[] { new { latitude = 8.77, longitude = -12.78 }, new { latitude = 8.78, longitude = -12.78 }, new { latitude = 8.78, longitude = -12.77 } };

        var bad = await client.PutAsJsonAsync($"/farms/{farmId}/map", new { latitude = 8.77, boundary = outline.Take(2) });
        Assert.Equal(HttpStatusCode.BadRequest, bad.StatusCode);

        var map = await ReadJsonAsync(await client.PutAsJsonAsync($"/farms/{farmId}/map", new { latitude = 8.775, longitude = -12.775, boundary = outline }));

        Assert.Equal(8.775, map["center"]!["latitude"]!.GetValue<double>());
        Assert.Equal(3, map["boundary"]!.AsArray().Count);
        Assert.Equal("Plot 1", Assert.Single(map["plots"]!.AsArray())!["plotName"]!.GetValue<string>());
    }

    [Fact]
    public async Task Health_shows_farm_scans_and_falls_back_to_the_latest_scan()
    {
        var (client, userId) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);
        var rice = await AddRiceAsync(client, farmId);

        var elsewhere = PlantScan.Create(userId, "/uploads/scans/a.jpg", DiagnosisResult.Create("Rice", null, 0.92, Severity.Healthy, "Looks healthy.", []));
        await factory.SeedAsync(elsewhere);
        var fallback = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/health"));
        Assert.False(fallback["lastScanIsFromThisFarm"]!.GetValue<bool>());
        Assert.Equal(("No major disease detected", 92), (fallback["lastScan"]!["headline"]!.GetValue<string>(), fallback["lastScan"]!["confidencePercent"]!.GetValue<int>()));

        var onFarm = PlantScan.Create(userId, "/uploads/scans/b.jpg",
            DiagnosisResult.Create("Rice", "Rice blast", 0.81, Severity.Moderate, "Leaf lesions.", []));
        await factory.SeedAsync(onFarm, FarmScan.Create(userId, farmId, rice["id"]!.GetValue<Guid>(), onFarm.Id));

        var health = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/health"));
        Assert.True(health["lastScanIsFromThisFarm"]!.GetValue<bool>());
        Assert.Equal(("Rice blast detected", "Rice (lowland)"), (health["lastScan"]!["headline"]!.GetValue<string>(), health["lastScan"]!["cropName"]!.GetValue<string>()));
        Assert.Equal(1, health["totalScans"]!.GetValue<int>());

        var overview = await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/overview"));
        Assert.False(overview["cropHealth"]!["isHealthy"]!.GetValue<bool>());

        using var notAnImage = new MultipartFormDataContent { { new StringContent("hello"), "image", "notes.txt" } };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/farms/{farmId}/health/scans", notAnImage)).StatusCode);
    }

    [Fact]
    public async Task Another_farmers_farm_is_not_found()
    {
        var (owner, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(owner);
        var rice = await AddRiceAsync(owner, farmId);
        var (other, _) = await SignedInClientAsync();

        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/farms/{farmId}/overview")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/farms/{farmId}/crops/{rice["id"]}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await other.PostAsJsonAsync($"/farms/{farmId}/activities", new { type = "other", title = "x", dueDate = Today })).StatusCode);
    }

    [Fact]
    public async Task Deleting_a_crop_keeps_completed_work()
    {
        var (client, _) = await SignedInClientAsync();
        var farmId = await AddFarmAsync(client);
        var rice = await AddRiceAsync(client, farmId);
        var cropId = rice["id"]!.GetValue<Guid>();
        var first = (await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/activities")))["items"]![0]!["id"]!.GetValue<Guid>();
        await client.PostAsync($"/farms/{farmId}/activities/{first}/complete", null);

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/farms/{farmId}/crops/{cropId}")).StatusCode);

        var remaining = (await ReadJsonAsync(await client.GetAsync($"/farms/{farmId}/activities")))["items"]!.AsArray();
        var kept = Assert.Single(remaining)!;
        Assert.Equal(("completed", "Rice (lowland)"), (kept["status"]!.GetValue<string>(), kept["cropName"]!.GetValue<string>()));
        Assert.Null(kept["farmCropId"]);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/farms/{farmId}/crops/{cropId}")).StatusCode);
    }
}
