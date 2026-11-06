using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Market;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Tests.AiTools;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgrismartAPI.Tests.Market;

public class MarketPricesEndpointsTests(AiToolsApiFactory factory) : IClassFixture<AiToolsApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private HttpClient ClientFor(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpClient> AdminClientAsync() =>
        ClientFor((await factory.CreateUserAsync(UserRole.Admin)).Token);

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    /// <summary>Each test uses its own crop, because the test class shares one database.</summary>
    private static string UniqueCrop() => $"Crop{Guid.NewGuid():N}"[..14];

    private static Task<HttpResponseMessage> RecordAsync(HttpClient client, DateOnly? date, params object[] prices) =>
        client.PostAsJsonAsync("/market-prices", new { priceDate = date, prices });

    private async Task<JsonArray> BoardItemsAsync(string crop, string? district = null)
    {
        var url = $"/market-prices?crop={crop}" + (district is null ? "" : $"&district={district}");
        return (await ReadJsonAsync(await factory.CreateClient().GetAsync(url)))["items"]!.AsArray();
    }

    private async Task<List<JsonNode>> TickerItemsAsync(string crop)
    {
        var ticker = await ReadJsonAsync(await factory.CreateClient().GetAsync("/market-prices/ticker"));
        return ticker["items"]!.AsArray().Where(item => item!["crop"]!.GetValue<string>().Equals(crop, StringComparison.OrdinalIgnoreCase)).ToList()!;
    }

    [Fact]
    public async Task Admin_records_a_price_and_anyone_can_read_it_without_a_token()
    {
        var crop = UniqueCrop();
        var response = await RecordAsync(await AdminClientAsync(), null, new { crop, district = "Bo", price = 450.5m, unit = "50 kg bag" });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await ReadJsonAsync(response);
        Assert.Equal(1, result["created"]!.GetValue<int>());
        Assert.Equal(0, result["updated"]!.GetValue<int>());

        var board = await ReadJsonAsync(await factory.CreateClient().GetAsync($"/market-prices?crop={crop}"));
        Assert.Equal("SLE", board["currency"]!.GetValue<string>());

        var item = Assert.Single(board["items"]!.AsArray())!;
        Assert.Equal("Bo", item["district"]!.GetValue<string>());
        Assert.Equal(450.5m, item["price"]!.GetValue<decimal>());
        Assert.Equal("50 kg bag", item["unit"]!.GetValue<string>());
        Assert.Equal("new", item["trend"]!.GetValue<string>());
        Assert.Null(item["change"]);
    }

    [Fact]
    public async Task Ticker_still_works_when_the_caller_sends_an_expired_or_invalid_token()
    {
        var response = await ClientFor("not-a-valid-token").GetAsync("/market-prices/ticker");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Seller)]
    [InlineData(UserRole.Buyer)]
    public async Task Only_admins_can_record_or_delete_prices(UserRole role)
    {
        var client = ClientFor((await factory.CreateUserAsync(role)).Token);

        var record = await RecordAsync(client, null, new { crop = UniqueCrop(), district = "Bo", price = 10, unit = "kg" });
        var delete = await client.DeleteAsync($"/market-prices/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, record.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Recording_requires_a_token()
    {
        var response = await RecordAsync(factory.CreateClient(), null, new { crop = UniqueCrop(), district = "Bo", price = 10, unit = "kg" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Recording_the_same_crop_district_and_day_replaces_the_price_whatever_the_spelling()
    {
        var crop = UniqueCrop();
        var admin = await AdminClientAsync();
        await RecordAsync(admin, Today, new { crop, district = "Freetown", price = 100, unit = "kg" });

        var response = await RecordAsync(admin, Today, new { crop = $"  {crop.ToUpperInvariant()} ", district = "Western Area Urban", price = 120, unit = "kg" });

        var result = await ReadJsonAsync(response);
        Assert.Equal(0, result["created"]!.GetValue<int>());
        Assert.Equal(1, result["updated"]!.GetValue<int>());
        var item = Assert.Single(await BoardItemsAsync(crop))!;
        Assert.Equal(120m, item["price"]!.GetValue<decimal>());
        Assert.Equal("Western Area Urban", item["district"]!.GetValue<string>());
        Assert.Equal("new", item["trend"]!.GetValue<string>());
    }

    [Fact]
    public async Task Board_shows_the_change_from_the_previous_price()
    {
        var crop = UniqueCrop();
        var admin = await AdminClientAsync();
        await RecordAsync(admin, Today.AddDays(-5), new { crop, district = "Kenema", price = 90, unit = "kg" });
        await RecordAsync(admin, Today.AddDays(-2), new { crop, district = "Kenema", price = 200, unit = "kg" });
        await RecordAsync(admin, Today, new { crop, district = "Kenema", price = 150, unit = "kg" });

        var item = Assert.Single(await BoardItemsAsync(crop))!;

        Assert.Equal(150m, item["price"]!.GetValue<decimal>());
        Assert.Equal(200m, item["previousPrice"]!.GetValue<decimal>());
        Assert.Equal(Today.AddDays(-2).ToString("yyyy-MM-dd"), item["previousDate"]!.GetValue<string>());
        Assert.Equal(-50m, item["change"]!.GetValue<decimal>());
        Assert.Equal(-25m, item["changePercent"]!.GetValue<decimal>());
        Assert.Equal("down", item["trend"]!.GetValue<string>());
    }

    [Fact]
    public async Task Prices_in_different_units_are_not_compared()
    {
        var crop = UniqueCrop();
        var admin = await AdminClientAsync();
        await RecordAsync(admin, Today.AddDays(-1), new { crop, district = "Bo", price = 500, unit = "50 kg bag" });
        await RecordAsync(admin, Today, new { crop, district = "Bo", price = 12, unit = "kg" });

        var item = Assert.Single(await BoardItemsAsync(crop))!;

        Assert.Equal("new", item["trend"]!.GetValue<string>());
        Assert.Null(item["previousPrice"]);
    }

    [Fact]
    public async Task Board_filters_by_district_and_lists_each_district_separately()
    {
        var crop = UniqueCrop();
        await RecordAsync(await AdminClientAsync(), null,
            new { crop, district = "Bo", price = 100, unit = "kg" },
            new { crop, district = "Bombali", price = 110, unit = "kg" });

        Assert.Equal(2, (await BoardItemsAsync(crop)).Count);
        var item = Assert.Single(await BoardItemsAsync(crop, "Bo"))!;
        Assert.Equal("Bo", item["district"]!.GetValue<string>());
    }

    [Fact]
    public async Task Ticker_leaves_out_prices_older_than_seven_days_and_the_board_keeps_them_for_thirty()
    {
        var (recent, week, stale, expired) = (UniqueCrop(), UniqueCrop(), UniqueCrop(), UniqueCrop());
        var admin = await AdminClientAsync();
        await RecordAsync(admin, Today.AddDays(-6), new { crop = week, district = "Bo", price = 10, unit = "kg" });
        await RecordAsync(admin, Today.AddDays(-7), new { crop = stale, district = "Bo", price = 10, unit = "kg" });
        await RecordAsync(admin, Today.AddDays(-30), new { crop = expired, district = "Bo", price = 10, unit = "kg" });
        await RecordAsync(admin, Today, new { crop = recent, district = "Bo", price = 10, unit = "kg" });

        Assert.Single(await TickerItemsAsync(recent));
        Assert.Single(await TickerItemsAsync(week));
        Assert.Empty(await TickerItemsAsync(stale));
        Assert.Single(await BoardItemsAsync(stale));
        Assert.Empty(await BoardItemsAsync(expired));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-valid-token")]
    public async Task Anyone_can_download_the_board_as_a_pdf(string? token)
    {
        var crop = UniqueCrop();
        await RecordAsync(await AdminClientAsync(), null, new { crop, district = "Bo", price = 450.5m, unit = "50 kg bag" });
        var client = token is null ? factory.CreateClient() : ClientFor(token);

        var response = await client.GetAsync($"/market-prices/pdf?crop={crop}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType!.MediaType);
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition!.DispositionType);
        Assert.Equal($"agrismart-crop-prices-{Today:yyyy-MM-dd}.pdf", response.Content.Headers.ContentDisposition.FileName);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString((await response.Content.ReadAsByteArrayAsync())[..5]));
    }

    [Fact]
    public async Task Pdf_is_still_a_valid_file_when_there_are_no_prices()
    {
        var response = await factory.CreateClient().GetAsync($"/market-prices/pdf?crop={UniqueCrop()}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith("%PDF-", Encoding.ASCII.GetString((await response.Content.ReadAsByteArrayAsync())[..5]));
    }

    [Fact]
    public async Task Pdf_uses_the_same_filters_and_date_as_the_board()
    {
        var renderer = new CapturingPdfRenderer();
        using var capturingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IMarketPricePdfRenderer>(renderer)));
        var crop = UniqueCrop();
        await RecordAsync(await AdminClientAsync(), Today.AddDays(-3),
            new { crop, district = "Bo", price = 100, unit = "kg" },
            new { crop, district = "Kenema", price = 110, unit = "kg" });
        await RecordAsync(await AdminClientAsync(), Today, new { crop, district = "Bo", price = 999, unit = "kg" });
        var date = Today.AddDays(-1);

        var response = await capturingFactory.CreateClient().GetAsync($"/market-prices/pdf?crop={crop.ToLowerInvariant()}&district=Bo&date={date:yyyy-MM-dd}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal($"agrismart-crop-prices-{date:yyyy-MM-dd}.pdf", response.Content.Headers.ContentDisposition!.FileName);
        Assert.Equal(MarketPriceCategory.Crop, renderer.Category);
        Assert.Equal("Bo", renderer.District);
        Assert.Equal(date, renderer.Board!.AsOf);
        Assert.Equal(100m, Assert.Single(renderer.Board.Items).Price);
    }

    [Fact]
    public async Task Pdf_rejects_an_unknown_district()
    {
        var response = await factory.CreateClient().GetAsync("/market-prices/pdf?district=Atlantis");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private sealed class CapturingPdfRenderer : IMarketPricePdfRenderer
    {
        public MarketPriceBoardDto? Board { get; private set; }
        public MarketPriceCategory? Category { get; private set; }
        public string? District { get; private set; }

        public byte[] Render(MarketPriceBoardDto board, MarketPriceCategory category, string? crop, string? district)
        {
            (Board, Category, District) = (board, category, district);
            return [];
        }
    }

    [Fact]
    public async Task The_same_name_can_be_a_crop_and_a_seed_and_the_board_shows_crops_unless_told_otherwise()
    {
        var name = UniqueCrop();
        var response = await RecordAsync(await AdminClientAsync(), Today,
            new { crop = name, district = "Bo", price = 100, unit = "50 kg bag" },
            new { crop = name, district = "Bo", price = 900, unit = "kg", category = "seed" });

        Assert.Equal(2, (await ReadJsonAsync(response))["created"]!.GetValue<int>());
        var crop = Assert.Single(await BoardItemsAsync(name))!;
        Assert.Equal("crop", crop["category"]!.GetValue<string>());
        Assert.Equal(100m, crop["price"]!.GetValue<decimal>());

        var board = await ReadJsonAsync(await factory.CreateClient().GetAsync($"/market-prices?category=seed&crop={name}"));
        var seed = Assert.Single(board["items"]!.AsArray())!;
        Assert.Equal("seed", seed["category"]!.GetValue<string>());
        Assert.Equal(900m, seed["price"]!.GetValue<decimal>());
        Assert.Equal("new", seed["trend"]!.GetValue<string>());
    }

    [Fact]
    public async Task Correcting_a_fertilizer_replaces_it_and_leaves_crops_alone()
    {
        var name = UniqueCrop();
        var admin = await AdminClientAsync();
        await RecordAsync(admin, Today, new { crop = name, district = "Bo", price = 100, unit = "kg", category = "fertilizer" });

        var response = await RecordAsync(admin, Today, new { crop = name, district = "Bo", price = 120, unit = "kg", category = "fertilizer" });

        Assert.Equal(1, (await ReadJsonAsync(response))["updated"]!.GetValue<int>());
        Assert.Empty(await BoardItemsAsync(name));
        var board = await ReadJsonAsync(await factory.CreateClient().GetAsync($"/market-prices?category=fertilizer&crop={name}"));
        Assert.Equal(120m, Assert.Single(board["items"]!.AsArray())!["price"]!.GetValue<decimal>());
    }

    [Theory]
    [InlineData("npk  15-15-15", "NPK 15-15-15")]
    [InlineData("DAP", "DAP")]
    [InlineData("nerica 4", "NERICA 4")]
    [InlineData("UREA", "Urea")]
    public async Task Fertilizer_and_seed_names_keep_their_capitals(string typed, string stored)
    {
        var district = "Moyamba";
        await RecordAsync(await AdminClientAsync(), Today, new { crop = typed, district, price = 10, unit = "kg", category = "fertilizer" });

        var board = await ReadJsonAsync(await factory.CreateClient().GetAsync($"/market-prices?category=fertilizer&district={district}&crop={Uri.EscapeDataString(typed)}"));

        Assert.Equal(stored, Assert.Single(board["items"]!.AsArray())!["crop"]!.GetValue<string>());
    }

    [Fact]
    public async Task Ticker_includes_every_category()
    {
        var (crop, fertilizer, seed) = (UniqueCrop(), UniqueCrop(), UniqueCrop());
        await RecordAsync(await AdminClientAsync(), Today,
            new { crop, district = "Bo", price = 1, unit = "kg" },
            new { crop = fertilizer, district = "Bo", price = 1, unit = "kg", category = "fertilizer" },
            new { crop = seed, district = "Bo", price = 1, unit = "kg", category = "seed" });

        Assert.Equal("crop", Assert.Single(await TickerItemsAsync(crop))["category"]!.GetValue<string>());
        Assert.Equal("fertilizer", Assert.Single(await TickerItemsAsync(fertilizer))["category"]!.GetValue<string>());
        Assert.Equal("seed", Assert.Single(await TickerItemsAsync(seed))["category"]!.GetValue<string>());
    }

    [Fact]
    public async Task History_reads_one_category()
    {
        var name = UniqueCrop();
        await RecordAsync(await AdminClientAsync(), Today,
            new { crop = name, district = "Kono", price = 1, unit = "kg" },
            new { crop = name, district = "Kono", price = 2, unit = "kg", category = "seed" });

        var crop = await ReadJsonAsync(await factory.CreateClient().GetAsync($"/market-prices/history?crop={name}&district=Kono"));
        var seed = await ReadJsonAsync(await factory.CreateClient().GetAsync($"/market-prices/history?crop={name}&district=Kono&category=seed"));

        Assert.Equal("crop", crop["category"]!.GetValue<string>());
        Assert.Equal(1m, Assert.Single(crop["points"]!.AsArray())!["price"]!.GetValue<decimal>());
        Assert.Equal("seed", seed["category"]!.GetValue<string>());
        Assert.Equal(2m, Assert.Single(seed["points"]!.AsArray())!["price"]!.GetValue<decimal>());
    }

    [Theory]
    [InlineData("fertilizer", MarketPriceCategory.Fertilizer)]
    [InlineData("seed", MarketPriceCategory.Seed)]
    public async Task Pdf_is_made_for_the_chosen_category(string category, MarketPriceCategory expected)
    {
        var renderer = new CapturingPdfRenderer();
        using var capturingFactory = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<IMarketPricePdfRenderer>(renderer)));

        var response = await capturingFactory.CreateClient().GetAsync($"/market-prices/pdf?category={category}&crop={UniqueCrop()}");

        Assert.Equal(expected, renderer.Category);
        Assert.Equal($"agrismart-{category}-prices-{Today:yyyy-MM-dd}.pdf", response.Content.Headers.ContentDisposition!.FileName);
    }

    [Theory]
    [InlineData("/market-prices?category=pesticide")]
    [InlineData("/market-prices/pdf?category=pesticide")]
    [InlineData("/market-prices/history?crop=Maize&district=Bo&category=pesticide")]
    public async Task Reading_rejects_an_unknown_category(string url)
    {
        var response = await factory.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("\"pesticide\"")]
    [InlineData("99")]
    public async Task Recording_rejects_an_unknown_category(string category)
    {
        var body = $$"""{ "prices": [{ "crop": "{{UniqueCrop()}}", "district": "Bo", "price": 10, "unit": "kg", "category": {{category}} }] }""";

        var response = await (await AdminClientAsync()).PostAsync("/market-prices", new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task History_returns_prices_oldest_first_within_the_requested_days()
    {
        var crop = UniqueCrop();
        var admin = await AdminClientAsync();
        await RecordAsync(admin, Today.AddDays(-10), new { crop, district = "Kono", price = 1, unit = "kg" });
        await RecordAsync(admin, Today.AddDays(-3), new { crop, district = "Kono", price = 2, unit = "kg" });
        await RecordAsync(admin, Today, new { crop, district = "Kono", price = 3, unit = "kg" });

        var history = await ReadJsonAsync(await factory.CreateClient().GetAsync($"/market-prices/history?crop={crop}&district=Koidu&days=7"));

        Assert.Equal("Kono", history["district"]!.GetValue<string>());
        Assert.Equal([2m, 3m], history["points"]!.AsArray().Select(point => point!["price"]!.GetValue<decimal>()));
    }

    [Theory]
    [InlineData("?district=Bo")]
    [InlineData("?crop=Maize")]
    [InlineData("?crop=Maize&district=Atlantis")]
    [InlineData("?crop=Maize&district=Bo&days=0")]
    [InlineData("?crop=Maize&district=Bo&days=366")]
    public async Task History_rejects_invalid_queries(string query)
    {
        var response = await factory.CreateClient().GetAsync($"/market-prices/history{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Board_rejects_an_unknown_district()
    {
        var response = await factory.CreateClient().GetAsync("/market-prices?district=Atlantis");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    public static TheoryData<object> InvalidEntries => new()
    {
        new { crop = "", district = "Bo", price = 10, unit = "kg" },
        new { crop = new string('x', MarketPrice.MaxCropLength + 1), district = "Bo", price = 10, unit = "kg" },
        new { crop = "Maize", district = "Atlantis", price = 10, unit = "kg" },
        new { crop = "Maize", district = "Bo", price = 0, unit = "kg" },
        new { crop = "Maize", district = "Bo", price = -5, unit = "kg" },
        new { crop = "Maize", district = "Bo", price = 0.001m, unit = "kg" },
        new { crop = "Maize", district = "Bo", price = 2_000_000_000m, unit = "kg" },
        new { crop = "Maize", district = "Bo", price = 10, unit = "" }
    };

    [Theory]
    [MemberData(nameof(InvalidEntries))]
    public async Task Recording_rejects_invalid_entries(object entry)
    {
        var response = await RecordAsync(await AdminClientAsync(), null, entry);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task One_invalid_entry_saves_nothing()
    {
        var crop = UniqueCrop();

        var response = await RecordAsync(await AdminClientAsync(), null,
            new { crop, district = "Bo", price = 10, unit = "kg" },
            new { crop, district = "Atlantis", price = 10, unit = "kg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("prices[1]", (await ReadJsonAsync(response))["message"]!.GetValue<string>());
        Assert.Empty(await BoardItemsAsync(crop));
    }

    [Fact]
    public async Task Recording_rejects_the_same_crop_and_district_twice_in_one_request()
    {
        var crop = UniqueCrop();

        var response = await RecordAsync(await AdminClientAsync(), null,
            new { crop, district = "Bo", price = 10, unit = "kg" },
            new { crop, district = "bo", price = 20, unit = "kg" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Recording_rejects_a_future_date_and_an_empty_list()
    {
        var admin = await AdminClientAsync();

        var future = await RecordAsync(admin, Today.AddDays(2), new { crop = UniqueCrop(), district = "Bo", price = 10, unit = "kg" });
        var empty = await RecordAsync(admin, null);

        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
    }

    [Fact]
    public async Task Admin_can_delete_a_price_and_the_previous_one_becomes_the_latest()
    {
        var crop = UniqueCrop();
        var admin = await AdminClientAsync();
        await RecordAsync(admin, Today.AddDays(-1), new { crop, district = "Bo", price = 100, unit = "kg" });
        await RecordAsync(admin, Today, new { crop, district = "Bo", price = 9999, unit = "kg" });
        var mistake = (await BoardItemsAsync(crop))[0]!["id"]!.GetValue<Guid>();

        var deleted = await admin.DeleteAsync($"/market-prices/{mistake}");
        var deletedAgain = await admin.DeleteAsync($"/market-prices/{mistake}");

        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, deletedAgain.StatusCode);
        Assert.Equal(100m, Assert.Single(await BoardItemsAsync(crop))!["price"]!.GetValue<decimal>());
    }
}
