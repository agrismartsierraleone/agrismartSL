using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace AgrismartAPI.Tests.Assistant;

public class FarmsEndpointsTests(AssistantApiFactory factory) : IClassFixture<AssistantApiFactory>
{
    private static readonly object RiverFarm = new
    {
        name = "River farm",
        district = "bo",
        latitude = 7.96,
        longitude = -11.74,
        area = 2,
        areaUnit = "acre",
        crops = new object[]
        {
            new { cropId = "maize", plantingDate = "2026-07-01", plotSize = 0.5 },
            new { cropId = "cassava" }
        }
    };

    private async Task<HttpClient> SignedInClientAsync()
    {
        var (_, token) = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Fact]
    public async Task Farms_require_sign_in()
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/farms")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/farms", RiverFarm)).StatusCode);
    }

    [Fact]
    public async Task Add_farm_with_crops_normalizes_district_and_converts_area()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/farms", RiverFarm);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var farm = await ReadJsonAsync(response);
        Assert.Equal("Bo", farm["district"]!.GetValue<string>());
        Assert.Equal(0.8094, farm["areaHectares"]!.GetValue<double>(), 4);
        var crops = farm["crops"]!.AsArray();
        Assert.Equal(["maize", "cassava"], crops.Select(c => c!["cropId"]!.GetValue<string>()));
        Assert.Equal("2026-07-01", crops[0]!["plantingDate"]!.GetValue<string>());

        var list = await ReadJsonAsync(await client.GetAsync("/farms"));
        Assert.Equal(farm["id"]!.GetValue<Guid>(), Assert.Single(list.AsArray())!["id"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Add_farm_returns_field_level_errors()
    {
        var client = await SignedInClientAsync();

        var response = await client.PostAsJsonAsync("/farms", new
        {
            name = "",
            district = "Atlantis",
            latitude = 8.4,
            area = -1,
            crops = new object[] { new { cropId = "unicorn-corn" } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJsonAsync(response))["errors"]!.AsObject();
        Assert.Equal(
            new[] { "area", "crops[0].cropId", "district", "latitude", "name" },
            errors.Select(e => e.Key).Order(StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public async Task Farmers_only_see_their_own_farms()
    {
        var owner = await SignedInClientAsync();
        var other = await SignedInClientAsync();
        await owner.PostAsJsonAsync("/farms", RiverFarm);

        Assert.Single((await ReadJsonAsync(await owner.GetAsync("/farms"))).AsArray());
        Assert.Empty((await ReadJsonAsync(await other.GetAsync("/farms"))).AsArray());
    }

    [Fact]
    public async Task Edit_farm_replaces_details_and_keeps_crops_when_omitted()
    {
        var client = await SignedInClientAsync();
        var farmId = (await ReadJsonAsync(await client.PostAsJsonAsync("/farms", RiverFarm)))["id"]!.GetValue<Guid>();

        var response = await client.PutAsJsonAsync($"/farms/{farmId}", new { name = "Swamp farm", district = "KENEMA", area = 1.5 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var farm = await ReadJsonAsync(response);
        Assert.Equal("Swamp farm", farm["name"]!.GetValue<string>());
        Assert.Equal("Kenema", farm["district"]!.GetValue<string>());
        Assert.Equal(1.5, farm["areaHectares"]!.GetValue<double>());
        Assert.Null(farm["latitude"]);
        Assert.Equal(2, farm["crops"]!.AsArray().Count);

        var saved = Assert.Single((await ReadJsonAsync(await client.GetAsync("/farms"))).AsArray())!;
        Assert.Equal("Swamp farm", saved["name"]!.GetValue<string>());
        Assert.Equal(2, saved["crops"]!.AsArray().Count);
    }

    [Fact]
    public async Task Edit_farm_replaces_crops_when_given()
    {
        var client = await SignedInClientAsync();
        var farmId = (await ReadJsonAsync(await client.PostAsJsonAsync("/farms", RiverFarm)))["id"]!.GetValue<Guid>();

        var response = await client.PutAsJsonAsync($"/farms/{farmId}", new
        {
            name = "River farm",
            crops = new object[] { new { cropId = "rice-lowland", plantingDate = "2026-06-15", plotSize = 5000, areaUnit = "squareMeter" } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var crop = Assert.Single((await ReadJsonAsync(response))["crops"]!.AsArray())!;
        Assert.Equal("rice-lowland", crop["cropId"]!.GetValue<string>());
        Assert.Equal(0.5, crop["plotSizeHectares"]!.GetValue<double>(), 4);

        var saved = Assert.Single((await ReadJsonAsync(await client.GetAsync("/farms"))).AsArray())!;
        Assert.Equal("rice-lowland", Assert.Single(saved["crops"]!.AsArray())!["cropId"]!.GetValue<string>());

        var cleared = await client.PutAsJsonAsync($"/farms/{farmId}", new { name = "River farm", crops = Array.Empty<object>() });
        Assert.Empty((await ReadJsonAsync(cleared))["crops"]!.AsArray());
    }

    [Fact]
    public async Task Edit_farm_validates_like_add()
    {
        var client = await SignedInClientAsync();
        var farmId = (await ReadJsonAsync(await client.PostAsJsonAsync("/farms", RiverFarm)))["id"]!.GetValue<Guid>();

        var response = await client.PutAsJsonAsync($"/farms/{farmId}", new { name = "", longitude = -11.7 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await ReadJsonAsync(response))["errors"]!.AsObject();
        Assert.True(errors.ContainsKey("name"));
        Assert.True(errors.ContainsKey("latitude"));
        Assert.Equal("River farm", Assert.Single((await ReadJsonAsync(await client.GetAsync("/farms"))).AsArray())!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Delete_farm_removes_it_and_its_crops()
    {
        var client = await SignedInClientAsync();
        var farmId = (await ReadJsonAsync(await client.PostAsJsonAsync("/farms", RiverFarm)))["id"]!.GetValue<Guid>();

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"/farms/{farmId}")).StatusCode);

        Assert.Empty((await ReadJsonAsync(await client.GetAsync("/farms"))).AsArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync($"/farms/{farmId}")).StatusCode);
    }

    [Fact]
    public async Task Other_farmers_cannot_edit_or_delete_a_farm()
    {
        var owner = await SignedInClientAsync();
        var other = await SignedInClientAsync();
        var farmId = (await ReadJsonAsync(await owner.PostAsJsonAsync("/farms", RiverFarm)))["id"]!.GetValue<Guid>();

        Assert.Equal(HttpStatusCode.NotFound, (await other.PutAsJsonAsync($"/farms/{farmId}", new { name = "Mine now" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/farms/{farmId}")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().DeleteAsync($"/farms/{farmId}")).StatusCode);

        Assert.Equal("River farm", Assert.Single((await ReadJsonAsync(await owner.GetAsync("/farms"))).AsArray())!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task Assistant_uses_the_farm_added_after_login()
    {
        var client = await SignedInClientAsync();
        await client.PostAsJsonAsync("/farms", RiverFarm);

        factory.ChatClient.Script = FakeChatClient.CallToolThenAnswer(
            "get_farm_profile",
            new Dictionary<string, object?>(),
            toolJson => toolJson.Contains("River farm") ? "Your River farm in Bo grows maize and cassava." : "No farm found.");

        var reply = await ReadJsonAsync(await client.PostAsJsonAsync("/api/assistant/chat", new { message = "What do I grow?" }));

        Assert.Equal("Your River farm in Bo grows maize and cassava.", reply["reply"]!.GetValue<string>());
    }
}
