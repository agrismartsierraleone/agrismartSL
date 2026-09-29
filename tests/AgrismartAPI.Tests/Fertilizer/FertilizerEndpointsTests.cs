using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Domain.Users;

namespace AgrismartAPI.Tests.Fertilizer;

public class FertilizerEndpointsTests(FertilizerApiFactory factory) : IClassFixture<FertilizerApiFactory>
{
    private static readonly object ValidMaizeRequest = new
    {
        cropId = "maize",
        area = 1,
        areaUnit = "acre",
        soil = new { fertility = "low", soilType = "sandy", ph = 5.0 },
        targetYieldTPerHa = 4.0,
        fertilizerProductIds = new[] { "npk-15-15-15", "urea" },
        customPrices = new[] { new { productId = "npk-15-15-15", pricePerBag = 950 }, new { productId = "urea", pricePerBag = 900 } },
        plotLabel = "River plot"
    };

    private async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Fact]
    public async Task Calculate_returns_a_camelCase_plan_with_string_enums()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/fertilizer/calculate", ValidMaizeRequest);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);

        Assert.Equal("Maize", body["crop"]!.GetValue<string>());
        Assert.Equal(0.4047, body["areaHectares"]!.GetValue<double>(), 4);
        Assert.Equal("farmerSelected", body["selectionMode"]!.GetValue<string>());
        Assert.Equal("low", body["soilAdjustment"]!["nitrogenClass"]!.GetValue<string>());
        Assert.NotNull(body["nutrientRequirementKg"]!["p2o5"]);
        Assert.NotNull(body["nutrientRequirementKg"]!["k2o"]);

        var products = body["products"]!.AsArray();
        Assert.Equal(["npk-15-15-15", "urea"], products.Select(p => p!["productId"]!.GetValue<string>()));
        Assert.All(products, p =>
        {
            Assert.True(p!["quantityKg"]!.GetValue<double>() > 0);
            Assert.True(p["bags50Kg"]!.GetValue<double>() >= 0.5);
        });
        Assert.Equal("compound", products[0]!["type"]!.GetValue<string>());
        Assert.True(body["costComplete"]!.GetValue<bool>());
        Assert.True(body["totalEstimatedCost"]!.GetValue<decimal>() > 0);
        Assert.Equal("SLE", body["currency"]!.GetValue<string>());

        Assert.NotEmpty(body["schedule"]!.AsArray());
        Assert.Contains(body["notes"]!.AsArray(), n => n!.GetValue<string>().Contains("agricultural lime"));
    }

    [Fact]
    public async Task Calculate_returns_field_level_400_for_invalid_input()
    {
        var client = factory.CreateClient();
        var request = new
        {
            cropId = "banana",
            area = 0,
            soil = new { mode = "soilTest", nitrogenTotalPercent = 0.1, phosphorusBray1MgPerKg = -2, potassiumCmolPerKg = 0.3 },
            fertilizerProductIds = new[] { "npk-99-99-99" }
        };

        var response = await client.PostAsJsonAsync("/fertilizer/calculate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.StartsWith("Validation failed", body["message"]!.GetValue<string>());

        var errors = body["errors"]!.AsObject();
        Assert.Contains("cropId", errors.Select(e => e.Key));
        Assert.Contains("area", errors.Select(e => e.Key));
        Assert.Contains("soil.phosphorusBray1MgPerKg", errors.Select(e => e.Key));
        Assert.Contains("fertilizerProductIds[0]", errors.Select(e => e.Key));
    }

    [Fact]
    public async Task Calculate_rejects_an_unknown_area_unit()
    {
        var client = factory.CreateClient();
        var request = new { cropId = "maize", area = 1, areaUnit = "furlong", soil = new { fertility = "medium" } };

        var response = await client.PostAsJsonAsync("/fertilizer/calculate", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Reference_data_is_seeded_and_missing_records_are_404()
    {
        var client = factory.CreateClient();

        var crops = await client.GetFromJsonAsync<JsonArray>("/fertilizer/crops");
        var products = await client.GetFromJsonAsync<JsonArray>("/fertilizer/products");
        var urea = await client.GetFromJsonAsync<JsonObject>("/fertilizer/products/urea");
        var missing = await client.GetAsync("/fertilizer/products/npk-99-99-99");

        Assert.Equal(11, crops!.Count);
        Assert.Equal(9, products!.Count);
        Assert.Equal(46, urea!["nitrogenPercent"]!.GetValue<double>());
        Assert.False(urea["hasPrice"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Saving_requires_sign_in()
    {
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/fertilizer/recommendations", ValidMaizeRequest);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Recommendations_can_be_saved_listed_read_and_deleted_by_their_owner_only()
    {
        var owner = factory.CreateClient();
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.CreateUserTokenAsync());
        var stranger = factory.CreateClient();
        stranger.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.CreateUserTokenAsync());

        var saved = await owner.PostAsJsonAsync("/fertilizer/recommendations", ValidMaizeRequest);
        Assert.Equal(HttpStatusCode.Created, saved.StatusCode);
        var savedBody = await ReadJsonAsync(saved);
        var id = savedBody["id"]!.GetValue<Guid>();
        Assert.Equal("River plot", savedBody["plotLabel"]!.GetValue<string>());
        Assert.Equal("maize", savedBody["inputs"]!["cropId"]!.GetValue<string>());
        Assert.Equal("Maize", savedBody["result"]!["crop"]!.GetValue<string>());

        var page = await owner.GetFromJsonAsync<JsonObject>("/fertilizer/recommendations?cropId=maize&plotLabel=river%20plot&pageSize=5");
        Assert.Equal(1, page!["totalCount"]!.GetValue<int>());
        Assert.Equal(id, page["items"]![0]!["id"]!.GetValue<Guid>());

        var strangerPage = await stranger.GetFromJsonAsync<JsonObject>("/fertilizer/recommendations");
        Assert.Equal(0, strangerPage!["totalCount"]!.GetValue<int>());
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/fertilizer/recommendations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.DeleteAsync($"/fertilizer/recommendations/{id}")).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync($"/fertilizer/recommendations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await owner.DeleteAsync($"/fertilizer/recommendations/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync($"/fertilizer/recommendations/{id}")).StatusCode);
    }

    [Fact]
    public async Task OpenApi_document_has_docs_and_examples_for_fertilizer_endpoints()
    {
        var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonObject>("/openapi/v1.json");

        var calculate = document!["paths"]!["/fertilizer/calculate"]!["post"]!;
        Assert.Equal("Calculates a fertilizer plan without saving it.", calculate["summary"]!.GetValue<string>());
        Assert.Equal("maize", calculate["requestBody"]!["content"]!["application/json"]!["example"]!["cropId"]!.GetValue<string>());
        Assert.Equal("Maize", calculate["responses"]!["200"]!["content"]!["application/json"]!["example"]!["crop"]!.GetValue<string>());
        Assert.NotNull(calculate["responses"]!["400"]!["content"]!["application/json"]!["example"]!["errors"]);

        foreach (var path in new[] { "/fertilizer/crops", "/fertilizer/products", "/fertilizer/recommendations" })
        {
            Assert.NotNull(document["paths"]![path]!["get"]!["responses"]!["200"]!["content"]!["application/json"]!["example"]);
        }
    }

    [Fact]
    public async Task Only_admins_can_edit_reference_data()
    {
        var buyer = factory.CreateClient();
        buyer.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.CreateUserTokenAsync());
        var admin = factory.CreateClient();
        admin.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await factory.CreateUserTokenAsync(UserRole.Admin));
        var update = new
        {
            name = "Muriate of Potash (0-0-60)", type = "straight",
            nitrogenPercent = 0, phosphatePercent = 0, potashPercent = 60, bagSizeKg = 50, pricePerBag = 1100
        };

        var forbidden = await buyer.PutAsJsonAsync("/fertilizer/products/mop", update);
        var updated = await admin.PutAsJsonAsync("/fertilizer/products/mop", update);

        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var body = await ReadJsonAsync(updated);
        Assert.Equal(1100m, body["pricePerBag"]!.GetValue<decimal>());
        Assert.True(body["hasPrice"]!.GetValue<bool>());
    }
}
