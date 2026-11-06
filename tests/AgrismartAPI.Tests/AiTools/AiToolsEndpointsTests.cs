using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Assistant;
using AgrismartAPI.Application.Logistics;
using AgrismartAPI.Application.Orders;
using AgrismartAPI.Domain.Jobs;
using AgrismartAPI.Domain.Logistics;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Persistence;
using AgrismartAPI.Tests.Assistant;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Tests.AiTools;

public class AiToolsEndpointsTests(AiToolsApiFactory factory) : IClassFixture<AiToolsApiFactory>
{
    private HttpClient ClientFor(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private void AiReplies(string text) =>
        factory.ChatClient.Script = (_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, text));

    private void AiIsDown() =>
        factory.ChatClient.Script = (_, _) => throw new InvalidOperationException("provider down");

    private static OrderItem Item(Seller seller, string name, decimal price, int quantity, Guid? productId = null) =>
        new(productId ?? Guid.NewGuid(), name, price, quantity, seller.Id, seller.Name);

    private async Task<Order> SeedOrderAsync(Guid buyerId, PaymentMethod method, string? district, string? address, params OrderItem[] items)
    {
        var order = Order.Create(buyerId, items, method, district, address);
        await factory.SeedAsync(order);
        return order;
    }

    private async Task<Category> SeedCategoryAsync()
    {
        var category = Category.Create($"Category {Guid.NewGuid():N}", null);
        await factory.SeedAsync(category);
        return category;
    }

    // ---------- Trade ----------

    [Fact]
    public async Task Listing_draft_resolves_the_category_and_saves_nothing()
    {
        var (_, _, token) = await factory.CreateSellerAsync();
        var category = await SeedCategoryAsync();
        AiReplies($$"""
            ```json
            {"title": "Local rice", "description": "Clean local rice.", "categoryName": "{{category.Name}}", "unit": "bag"}
            ```
            """);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var productsBefore = await dbContext.MarketProducts.CountAsync();

        var response = await ClientFor(token).PostAsync("/api/ai/listing-draft", new MultipartFormDataContent { { new StringContent("rice from Bo"), "notes" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Local rice", body["title"]!.GetValue<string>());
        Assert.Equal(category.Id, body["categoryId"]!.GetValue<Guid>());
        Assert.Equal(productsBefore, await dbContext.MarketProducts.CountAsync());
    }

    [Fact]
    public async Task Listing_draft_sends_the_photo_to_the_model()
    {
        var (_, _, token) = await factory.CreateSellerAsync();
        AiReplies("""{"title": "Maize", "description": "Dry maize.", "categoryName": null, "unit": null}""");
        var image = new ByteArrayContent([1, 2, 3]);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");

        var response = await ClientFor(token).PostAsync("/api/ai/listing-draft", new MultipartFormDataContent { { image, "image", "maize.jpg" } });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(factory.ChatClient.Calls[^1].SelectMany(message => message.Contents), content => content is DataContent);
    }

    [Fact]
    public async Task Listing_draft_needs_notes_or_a_photo_and_is_not_for_buyers()
    {
        var (_, _, sellerToken) = await factory.CreateSellerAsync();
        var (_, buyerToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var form = () => new MultipartFormDataContent { { new StringContent(" "), "notes" } };

        Assert.Equal(HttpStatusCode.BadRequest, (await ClientFor(sellerToken).PostAsync("/api/ai/listing-draft", form())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(buyerToken).PostAsync("/api/ai/listing-draft", form())).StatusCode);
    }

    [Fact]
    public async Task Price_suggestion_uses_the_median_of_real_prices_even_if_the_ai_says_otherwise()
    {
        var (seller, _, token) = await factory.CreateSellerAsync();
        var category = await SeedCategoryAsync();
        await factory.SeedAsync(
            MarketProduct.Create("Local rice", category.Id, 100, "bag", seller.Id, "d"),
            MarketProduct.Create("Rice premium", category.Id, 300, "bag", seller.Id, "d"),
            MarketProduct.Create("Rice husked", category.Id, 200, "bag", seller.Id, "d"),
            MarketProduct.Create("Rice sold by the cup", category.Id, 1, "cup", seller.Id, "d"),
            MarketProduct.Create("Cassava", category.Id, 900, "bag", seller.Id, "d"));
        AiReplies("Charge 99999.");

        var response = await ClientFor(token).PostAsJsonAsync("/api/ai/price-suggestion", new { productName = "rice", categoryId = category.Id, unit = "bag" });

        var body = await ReadJsonAsync(response);
        Assert.Equal(200m, body["suggestedPrice"]!.GetValue<decimal>());
        Assert.Equal(100m, body["lowestPrice"]!.GetValue<decimal>());
        Assert.Equal(300m, body["highestPrice"]!.GetValue<decimal>());
        Assert.Equal(3, body["comparableListings"]!.GetValue<int>());
    }

    [Fact]
    public async Task Price_suggestion_gives_no_price_without_enough_data_and_still_works_if_the_ai_is_down()
    {
        var (seller, _, token) = await factory.CreateSellerAsync();
        var category = await SeedCategoryAsync();
        var client = ClientFor(token);
        await factory.SeedAsync(MarketProduct.Create("Ginger root", category.Id, 50, "kg", seller.Id, "d"));

        var tooFew = await ReadJsonAsync(await client.PostAsJsonAsync("/api/ai/price-suggestion", new { productName = "ginger", categoryId = category.Id }));
        Assert.Null(tooFew["suggestedPrice"]);

        await factory.SeedAsync(
            MarketProduct.Create("Ginger dried", category.Id, 70, "kg", seller.Id, "d"),
            MarketProduct.Create("Ginger fresh", category.Id, 60, "kg", seller.Id, "d"));
        AiIsDown();

        var response = await client.PostAsJsonAsync("/api/ai/price-suggestion", new { productName = "ginger", categoryId = category.Id });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(60m, body["suggestedPrice"]!.GetValue<decimal>());
        Assert.Contains("between 50 and 70", body["explanation"]!.GetValue<string>());
    }

    // ---------- Payments ----------

    [Fact]
    public async Task Earnings_count_only_the_sellers_share_and_skip_cancelled_orders()
    {
        var (seller, _, token) = await factory.CreateSellerAsync();
        var (otherSeller, _, _) = await factory.CreateSellerAsync();
        var (buyer, _) = await factory.CreateUserAsync(UserRole.Buyer);

        var paid = Order.Create(buyer.Id, [Item(seller, "Rice", 100, 2), Item(otherSeller, "Yam", 999, 1)], PaymentMethod.MonimeOnline);
        paid.MarkSellerPaid(seller.Id, null);
        var pending = Order.Create(buyer.Id, [Item(seller, "Rice", 50, 1)], PaymentMethod.CashOnPickup);
        var cancelled = Order.Create(buyer.Id, [Item(seller, "Rice", 7000, 1)], PaymentMethod.UnpaidHold);
        cancelled.CancelUnpaid();
        await factory.SeedAsync(paid, pending, cancelled);
        AiIsDown();

        var response = await ClientFor(token).GetAsync("/api/ai/earnings-summary?days=30");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var earnings = body["earnings"]!;
        Assert.Equal(2, earnings["orders"]!.GetValue<int>());
        Assert.Equal(200m, earnings["received"]!.GetValue<decimal>());
        Assert.Equal(50m, earnings["pending"]!.GetValue<decimal>());
        Assert.Equal("Rice", Assert.Single(earnings["topProducts"]!.AsArray())!["productName"]!.GetValue<string>());
        Assert.Contains("200", body["summary"]!.GetValue<string>());
    }

    [Fact]
    public async Task Order_alerts_flag_duplicates_for_admins_without_changing_the_order()
    {
        var (seller, _, _) = await factory.CreateSellerAsync();
        var (buyer, buyerToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var (_, adminToken) = await factory.CreateUserAsync(UserRole.Admin);
        var productId = Guid.NewGuid();
        var first = await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, null, null, Item(seller, "Rice", 10, 1, productId));
        var second = await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, null, null, Item(seller, "Rice", 10, 1, productId));

        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(buyerToken).GetAsync("/orders/alerts")).StatusCode);

        var alerts = (await ReadJsonAsync(await ClientFor(adminToken).GetAsync("/orders/alerts"))).AsArray();

        var alertedIds = alerts.Select(alert => alert!["orderId"]!.GetValue<Guid>()).ToList();
        Assert.True(alertedIds.Contains(first.Id) || alertedIds.Contains(second.Id));

        using var scope = factory.Services.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Orders.SingleAsync(order => order.Id == second.Id);
        Assert.Equal(OrderStatus.Pending, stored.Status);
    }

    [Fact]
    public async Task Order_alerts_flag_a_seller_buying_their_own_goods()
    {
        var (seller, sellerUser, _) = await factory.CreateSellerAsync();
        var (_, adminToken) = await factory.CreateUserAsync(UserRole.Admin);
        var order = await SeedOrderAsync(sellerUser.Id, PaymentMethod.CashOnPickup, null, null, Item(seller, "Rice", 10, 1));

        var alerts = (await ReadJsonAsync(await ClientFor(adminToken).GetAsync("/orders/alerts"))).AsArray();

        var alert = alerts.Single(entry => entry!["orderId"]!.GetValue<Guid>() == order.Id)!;
        Assert.Contains("also the seller", alert["reasons"]![0]!.GetValue<string>());
    }

    // ---------- Logistics ----------

    [Fact]
    public async Task Cash_order_moves_forward_through_delivery_and_never_backwards()
    {
        var (seller, _, token) = await factory.CreateSellerAsync();
        var (buyer, _) = await factory.CreateUserAsync(UserRole.Buyer);
        var order = await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, "Bo", null, Item(seller, "Rice", 10, 1));
        var client = ClientFor(token);
        var url = $"/orders/{order.Id}/delivery-status";

        var shipped = await client.PutAsJsonAsync(url, new { status = "shipped" });
        Assert.Equal(HttpStatusCode.OK, shipped.StatusCode);
        Assert.NotNull((await ReadJsonAsync(shipped))["shippedAt"]);

        Assert.Equal(HttpStatusCode.Conflict, (await client.PutAsJsonAsync(url, new { status = "confirmed" })).StatusCode);

        var delivered = await ReadJsonAsync(await client.PutAsJsonAsync(url, new { status = "delivered" }));
        Assert.Equal("delivered", delivered["status"]!.GetValue<string>(), ignoreCase: true);
        Assert.NotNull(delivered["deliveredAt"]);
    }

    [Theory]
    [InlineData(PaymentMethod.UnpaidHold)]
    [InlineData(PaymentMethod.MonimeOnline)]
    public async Task Unpaid_orders_cannot_be_confirmed_or_sent(PaymentMethod method)
    {
        var (seller, _, token) = await factory.CreateSellerAsync();
        var (buyer, _) = await factory.CreateUserAsync(UserRole.Buyer);
        var order = await SeedOrderAsync(buyer.Id, method, null, null, Item(seller, "Rice", 10, 1));

        var response = await ClientFor(token).PutAsJsonAsync($"/orders/{order.Id}/delivery-status", new { status = "confirmed" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Sellers_can_only_update_orders_that_are_entirely_theirs()
    {
        var (seller, _, sellerToken) = await factory.CreateSellerAsync();
        var (otherSeller, _, otherToken) = await factory.CreateSellerAsync();
        var (_, _, strangerToken) = await factory.CreateSellerAsync();
        var (_, adminToken) = await factory.CreateUserAsync(UserRole.Admin);
        var (buyer, buyerToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var order = await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, null, null, Item(seller, "Rice", 10, 1), Item(otherSeller, "Yam", 10, 1));
        var url = $"/orders/{order.Id}/delivery-status";
        var body = new { status = "shipped" };

        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(sellerToken).PutAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(otherToken).PutAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ClientFor(strangerToken).PutAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(buyerToken).PutAsJsonAsync(url, body)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(adminToken).PutAsJsonAsync(url, body)).StatusCode);
    }

    [Fact]
    public async Task Placing_an_order_stores_the_standard_district_name_and_rejects_unknown_ones()
    {
        var (seller, _, _) = await factory.CreateSellerAsync();
        var (_, buyerToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var category = await SeedCategoryAsync();
        var product = MarketProduct.Create("Rice", category.Id, 10, "bag", seller.Id, "d");
        await factory.SeedAsync(product);
        var client = ClientFor(buyerToken);
        var items = new[] { new { productId = product.Id, quantity = 1 } };

        var created = await client.PostAsJsonAsync("/orders", new { items, paymentMethod = "cashOnPickup", deliveryDistrict = "freetown", deliveryAddress = "12 Main Road" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal("Western Area Urban", (await ReadJsonAsync(created))["deliveryDistrict"]!.GetValue<string>());

        var rejected = await client.PostAsJsonAsync("/orders", new { items, paymentMethod = "cashOnPickup", deliveryDistrict = "Paris" });
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task Delivery_plan_groups_ready_orders_and_keeps_buyer_details_away_from_the_ai()
    {
        var (seller, _, token) = await factory.CreateSellerAsync("Bo town");
        var (otherSeller, _, _) = await factory.CreateSellerAsync();
        var (buyer, _) = await factory.CreateUserAsync(UserRole.Buyer, "+23276000111");

        await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, "Kenema", "Secret Street 1", Item(seller, "Rice", 10, 1), Item(otherSeller, "Hidden yam", 10, 1));
        await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, "Kenema", "Secret Street 2", Item(seller, "Rice", 10, 1));
        await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, "Kambia", null, Item(seller, "Rice", 10, 1));
        await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, null, null, Item(seller, "Rice", 10, 1));
        await SeedOrderAsync(buyer.Id, PaymentMethod.UnpaidHold, "Kenema", null, Item(seller, "Unpaid rice", 10, 1));
        await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, "Kenema", null, Item(otherSeller, "Not mine", 10, 1));
        AiReplies("Send the Kenema orders together.");

        var response = await ClientFor(token).GetAsync("/api/ai/delivery-plan");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var body = JsonNode.Parse(raw)!;
        var trips = body["trips"]!.AsArray();

        Assert.Equal("Bo", body["fromDistrict"]!.GetValue<string>());
        Assert.Equal(["Kenema", "Kambia"], trips.Select(trip => trip!["district"]!.GetValue<string>()));
        Assert.Equal(2, trips[0]!["stops"]!.AsArray().Count);
        Assert.Single(body["missingDistrict"]!.AsArray());
        Assert.Equal("Send the Kenema orders together.", body["advice"]!.GetValue<string>());
        Assert.Contains("+23276000111", raw);
        Assert.DoesNotContain("Hidden yam", raw);
        Assert.DoesNotContain("Unpaid rice", raw);
        Assert.DoesNotContain("Not mine", raw);

        var sentToAi = string.Join(" ", factory.ChatClient.Calls[^1].Select(message => message.Text));
        Assert.DoesNotContain("+23276000111", sentToAi);
        Assert.DoesNotContain("Secret Street", sentToAi);
        Assert.DoesNotContain(buyer.Name, sentToAi);
    }

    [Fact]
    public async Task Delivery_estimate_is_calculated_from_the_rates_for_any_signed_in_user()
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Buyer);

        var response = await ClientFor(token).PostAsJsonAsync("/logistics/delivery-estimate", new { fromDistrict = "Kenema", toDistrict = "Bo", weightKg = 10 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        var distance = body["approximateDistanceKm"]!.GetValue<double>();
        Assert.InRange(distance, 60, 110);
        Assert.Equal(100 + 2 * distance + 10, (double)body["estimatedCost"]!.GetValue<decimal>(), 0);
    }

    [Fact]
    public void Delivery_estimate_refuses_to_quote_until_rates_are_set()
    {
        var service = new DeliveryEstimateService(Options.Create(new DeliveryOptions()));

        Assert.Throws<AgrismartAPI.Application.Common.ConflictException>(() => service.Estimate(new DeliveryEstimateRequest("Bo", "Kenema", 10)));
    }

    [Theory]
    [InlineData("Bo", "Bo")]
    [InlineData("Bombali", "Bombali")]
    [InlineData("Bonthe town", "Bonthe")]
    [InlineData("near Makeni", "Bombali")]
    [InlineData("freetown", "Western Area Urban")]
    [InlineData("Boajibu", null)]
    [InlineData("", null)]
    public void District_lookup_matches_whole_words(string text, string? expected) =>
        Assert.Equal(expected, SierraLeoneDistricts.Find(text)?.Name);

    // ---------- Jobs ----------

    private static object JobBody(string district = "Kenema", string[]? skills = null) => new
    {
        title = "Rice harvesters",
        description = "Harvest two acres.",
        district,
        workersNeeded = 5,
        skills = skills ?? ["harvesting"],
        pay = "Agreed daily"
    };

    private static object WorkerBody(string district = "Kenema") => new
    {
        headline = "Harvest worker",
        bio = "Ten seasons of rice harvesting.",
        district,
        skills = new[] { "Harvesting", "Weeding" }
    };

    [Fact]
    public async Task Job_board_covers_posting_applying_and_closing()
    {
        var (_, posterToken) = await factory.CreateUserAsync(UserRole.Admin);
        var (_, workerToken) = await factory.CreateUserAsync(UserRole.Buyer, "+23277111222");
        var (_, otherToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var poster = ClientFor(posterToken);
        var worker = ClientFor(workerToken);

        var created = await poster.PostAsJsonAsync("/jobs", JobBody());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var jobId = (await ReadJsonAsync(created))["id"]!.GetValue<Guid>();

        var found = (await ReadJsonAsync(await worker.GetAsync("/jobs?district=kenema&skill=harvest"))).AsArray();
        Assert.Contains(found, job => job!["id"]!.GetValue<Guid>() == jobId);

        Assert.Equal(HttpStatusCode.BadRequest, (await worker.PostAsJsonAsync($"/jobs/{jobId}/apply", new { message = "Hi" })).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await worker.PutAsJsonAsync("/jobs/worker-profile", WorkerBody())).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await worker.PostAsJsonAsync($"/jobs/{jobId}/apply", new { message = "Hi" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await worker.PostAsJsonAsync($"/jobs/{jobId}/apply", new { message = "Again" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await poster.PostAsJsonAsync($"/jobs/{jobId}/apply", new { })).StatusCode);

        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(otherToken).GetAsync($"/jobs/{jobId}/applicants")).StatusCode);
        var applicants = (await ReadJsonAsync(await poster.GetAsync($"/jobs/{jobId}/applicants"))).AsArray();
        Assert.Equal("+23277111222", Assert.Single(applicants)!["phoneNumber"]!.GetValue<string>());

        Assert.Equal(HttpStatusCode.Forbidden, (await worker.PostAsync($"/jobs/{jobId}/close", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await poster.PostAsync($"/jobs/{jobId}/close", null)).StatusCode);

        await ClientFor(otherToken).PutAsJsonAsync("/jobs/worker-profile", WorkerBody());
        Assert.Equal(HttpStatusCode.Conflict, (await ClientFor(otherToken).PostAsJsonAsync($"/jobs/{jobId}/apply", new { })).StatusCode);
    }

    [Theory]
    [InlineData(UserRole.Buyer)]
    [InlineData(UserRole.Seller)]
    public async Task Only_admins_can_post_or_draft_a_job(UserRole role)
    {
        var (_, token) = await factory.CreateUserAsync(role);
        var client = ClientFor(token);

        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/jobs", JobBody())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsJsonAsync("/api/ai/job-draft", new { notes = "need help" })).StatusCode);
    }

    [Theory]
    [InlineData("", "Bo", 1)]
    [InlineData("Title", "Paris", 1)]
    [InlineData("Title", "Bo", 0)]
    public async Task Job_posts_are_validated(string title, string district, int workersNeeded)
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Admin);

        var response = await ClientFor(token).PostAsJsonAsync("/jobs", new { title, description = "Work", district, workersNeeded });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Job_draft_returns_a_clean_draft_and_saves_nothing()
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Admin);
        AiReplies("""{"title": "Rice harvest help", "description": "Five people to harvest.", "district": "Kenema town", "workersNeeded": 5, "skills": ["harvesting"]}""");

        var response = await ClientFor(token).PostAsJsonAsync("/api/ai/job-draft", new { notes = "need 5 people to harvest rice in Kenema" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal("Kenema", body["district"]!.GetValue<string>());
        Assert.Equal(5, body["workersNeeded"]!.GetValue<int>());
        Assert.Null(body["pay"]);
    }

    [Fact]
    public async Task A_reply_that_is_not_json_returns_503()
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Admin);
        AiReplies("Sorry, I cannot do that.");

        var response = await ClientFor(token).PostAsJsonAsync("/api/ai/job-draft", new { notes = "need help" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Worker_profile_draft_uses_the_workers_district_not_the_models()
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Buyer);
        AiReplies("""{"headline": "Experienced harvester", "bio": "Ten seasons.", "skills": ["harvesting"]}""");

        var response = await ClientFor(token).PostAsJsonAsync("/api/ai/worker-profile-draft", new { experience = "Ten seasons of harvest work", district = "Kono" });

        var body = await ReadJsonAsync(response);
        Assert.Equal("Experienced harvester", body["headline"]!.GetValue<string>());
        Assert.Equal("Kono", body["district"]!.GetValue<string>());
    }

    [Fact]
    public async Task Worker_matches_rank_by_fit_hide_phone_numbers_and_survive_an_ai_outage()
    {
        var (_, posterToken) = await factory.CreateUserAsync(UserRole.Admin);
        var (_, strangerToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var skill = $"skill-{Guid.NewGuid():N}";
        var jobId = (await ReadJsonAsync(await ClientFor(posterToken).PostAsJsonAsync("/jobs", JobBody("Moyamba", [skill]))))["id"]!.GetValue<Guid>();

        var (best, bestToken) = await factory.CreateUserAsync(UserRole.Buyer, "+23278999000");
        var (nearby, nearbyToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var (_, poorFitToken) = await factory.CreateUserAsync(UserRole.Buyer);
        await ClientFor(bestToken).PutAsJsonAsync("/jobs/worker-profile", new { headline = "h", bio = "b", district = "Moyamba", skills = new[] { skill } });
        await ClientFor(nearbyToken).PutAsJsonAsync("/jobs/worker-profile", new { headline = "h", bio = "b", district = "Moyamba", skills = Array.Empty<string>() });
        await ClientFor(poorFitToken).PutAsJsonAsync("/jobs/worker-profile", new { headline = "h", bio = "b", district = "Kambia", skills = Array.Empty<string>() });
        AiIsDown();

        Assert.Equal(HttpStatusCode.Forbidden, (await ClientFor(strangerToken).GetAsync($"/api/ai/jobs/{jobId}/matches")).StatusCode);

        var response = await ClientFor(posterToken).GetAsync($"/api/ai/jobs/{jobId}/matches");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var raw = await response.Content.ReadAsStringAsync();
        var matches = JsonNode.Parse(raw)!.AsArray();
        Assert.Equal(best.Id, matches[0]!["workerUserId"]!.GetValue<Guid>());
        Assert.Contains(matches, match => match!["workerUserId"]!.GetValue<Guid>() == nearby.Id);
        Assert.Contains(skill, matches[0]!["reason"]!.GetValue<string>());
        Assert.DoesNotContain("+23278999000", raw);
    }

    // ---------- Assistant tools ----------

    private sealed class StubCurrentUser(Guid userId) : ICurrentUserService
    {
        public bool IsAuthenticated => true;
        public Guid UserId => userId;
        public string Email => "stub@test.local";
        public UserRole Role => UserRole.Buyer;
    }

    private static AssistantTools ToolsFor(IServiceScope scope, Guid userId) =>
        ActivatorUtilities.CreateInstance<AssistantTools>(scope.ServiceProvider, new StubCurrentUser(userId));

    [Fact]
    public async Task Order_tool_returns_only_the_callers_orders()
    {
        var (seller, _, _) = await factory.CreateSellerAsync();
        var (buyer, _) = await factory.CreateUserAsync(UserRole.Buyer);
        var (other, _) = await factory.CreateUserAsync(UserRole.Buyer);
        await SeedOrderAsync(buyer.Id, PaymentMethod.CashOnPickup, "Bo", null, Item(seller, "My rice", 10, 2));
        await SeedOrderAsync(other.Id, PaymentMethod.CashOnPickup, "Bo", null, Item(seller, "Their yam", 10, 1));

        using var scope = factory.Services.CreateScope();
        var result = await ToolsFor(scope, buyer.Id).GetMyOrdersAsync();

        var order = Assert.Single(result.Orders);
        Assert.Equal("2 x My rice", Assert.Single(order.Items));
        Assert.Equal("Pending", order.DeliveryStatus);
        Assert.Equal("Bo", order.DeliveryDistrict);
    }

    [Fact]
    public async Task Product_tool_prefers_the_requested_district_and_rejects_unknown_places()
    {
        var (farSeller, _, _) = await factory.CreateSellerAsync("Kambia");
        var (nearSeller, _, _) = await factory.CreateSellerAsync("Bo");
        var category = await SeedCategoryAsync();
        var name = $"Fonio{Guid.NewGuid():N}";
        await factory.SeedAsync(
            MarketProduct.Create(name, category.Id, 5, "kg", farSeller.Id, "d"),
            MarketProduct.Create(name, category.Id, 9, "kg", nearSeller.Id, "d"));

        using var scope = factory.Services.CreateScope();
        var tools = ToolsFor(scope, Guid.NewGuid());

        var results = Assert.IsType<ProductSearchResults>(await tools.SearchProductsAsync(name, "Bo"));

        Assert.Equal(2, results.Products.Count);
        Assert.True(results.Products[0].InRequestedDistrict);
        Assert.Equal(9m, results.Products[0].Price);
        Assert.IsType<ToolError>(await tools.SearchProductsAsync(name, "Paris"));
    }

    [Fact]
    public async Task Earnings_tool_refuses_users_who_are_not_sellers()
    {
        var (buyer, _) = await factory.CreateUserAsync(UserRole.Buyer);

        using var scope = factory.Services.CreateScope();

        Assert.IsType<ToolError>(await ToolsFor(scope, buyer.Id).GetMySellerEarningsAsync());
    }

    [Fact]
    public void Earnings_ignore_orders_older_than_the_period()
    {
        var sellerId = Guid.NewGuid();
        var order = Order.Create(Guid.NewGuid(), [new OrderItem(Guid.NewGuid(), "Rice", 10, 1, sellerId, "S")], PaymentMethod.CashOnPickup);

        var earnings = SellerEarningsCalculator.Calculate([order], sellerId, 30, DateTimeOffset.UtcNow.AddDays(40));

        Assert.Equal(0, earnings.Orders);
    }

    [Fact]
    public async Task Deleting_a_job_removes_its_applications()
    {
        var (_, posterToken) = await factory.CreateUserAsync(UserRole.Admin);
        var (_, workerToken) = await factory.CreateUserAsync(UserRole.Buyer);
        var poster = ClientFor(posterToken);
        var worker = ClientFor(workerToken);
        var jobId = (await ReadJsonAsync(await poster.PostAsJsonAsync("/jobs", JobBody())))["id"]!.GetValue<Guid>();
        await worker.PutAsJsonAsync("/jobs/worker-profile", WorkerBody());
        await worker.PostAsJsonAsync($"/jobs/{jobId}/apply", new { });

        Assert.Equal(HttpStatusCode.NoContent, (await poster.DeleteAsync($"/jobs/{jobId}")).StatusCode);

        Assert.Equal(HttpStatusCode.NotFound, (await poster.GetAsync($"/jobs/{jobId}")).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Set<JobPost>().AnyAsync(job => job.Id == jobId));
    }
}

public class AiToolsWhenDisabledApiFactory : AiToolsApiFactory
{
    protected override bool AssistantEnabled => false;
}

public class AiToolsDisabledTests(AiToolsWhenDisabledApiFactory factory) : IClassFixture<AiToolsWhenDisabledApiFactory>
{
    [Fact]
    public async Task Ai_tools_return_404_but_jobs_and_logistics_keep_working()
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Admin);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/ai/job-draft", new { notes = "help" })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/jobs")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/logistics/districts")).StatusCode);
    }
}
