using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Persistence;
using AgrismartAPI.Tests.AiTools;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AgrismartAPI.Tests.Jobs;

public class WorkerProfileAccountTests(AiToolsApiFactory factory) : IClassFixture<AiToolsApiFactory>
{
    private const string Password = "original-pass";
    private const string Phone = "+23276111222";
    private const string Nin = "NIN123";

    private static string NewEmail() => $"{Guid.NewGuid():N}@test.local";

    private static object Profile(string? phoneNumber = null, string? nin = null, string headline = "Harvest worker") => new
    {
        headline,
        bio = "Ten seasons",
        district = "Bo",
        skills = new[] { "harvesting" },
        isAvailable = true,
        phoneNumber,
        nin
    };

    private async Task<(HttpClient Client, string Email)> RegisterAsync()
    {
        var email = NewEmail();
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/register", new { name = "Worker", email, password = Password, role = "Buyer", phoneNumber = Phone, nin = Nin });
        response.EnsureSuccessStatusCode();
        var token = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["token"]!.GetValue<string>();
        return (ClientFor(token), email);
    }

    private HttpClient ClientFor(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private async Task<HttpStatusCode> LoginAsync(string email, string password) =>
        (await factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, password })).StatusCode;

    private static async Task<JsonNode> ReadAsync(HttpResponseMessage response) => JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Theory]
    [InlineData(UserRole.Buyer)]
    [InlineData(UserRole.Seller)]
    public async Task Buyers_and_sellers_can_save_a_worker_profile(UserRole role)
    {
        var (_, token) = await factory.CreateUserAsync(role);

        var saved = await ClientFor(token).PutAsJsonAsync("/jobs/worker-profile", Profile());

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
    }

    [Fact]
    public async Task Admins_cannot_save_a_worker_profile()
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Admin);
        var admin = ClientFor(token);

        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PutAsJsonAsync("/jobs/worker-profile", Profile())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/jobs/worker-profile")).StatusCode);
    }

    [Fact]
    public async Task Profile_shows_the_phone_email_and_nin_from_the_account_and_never_a_password()
    {
        var (client, email) = await RegisterAsync();

        var saved = await client.PutAsJsonAsync("/jobs/worker-profile", new { headline = "Harvest worker", bio = "Ten seasons", district = "Bo", skills = new[] { "harvesting" } });
        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);

        var body = await client.GetStringAsync("/jobs/worker-profile");
        var profile = JsonNode.Parse(body)!;
        Assert.Equal(Phone, profile["phoneNumber"]!.GetValue<string>());
        Assert.Equal(email, profile["email"]!.GetValue<string>());
        Assert.Equal(Nin, profile["nin"]!.GetValue<string>());
        Assert.DoesNotContain("password", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Phone_number_and_nin_can_be_changed()
    {
        var (client, _) = await RegisterAsync();

        var saved = await ReadAsync(await client.PutAsJsonAsync("/jobs/worker-profile", Profile(phoneNumber: " +23277000000 ", nin: " NIN999 ")));

        Assert.Equal("+23277000000", saved["phoneNumber"]!.GetValue<string>());
        Assert.Equal("NIN999", saved["nin"]!.GetValue<string>());
    }

    [Fact]
    public async Task A_password_sent_to_the_profile_is_ignored()
    {
        var (client, email) = await RegisterAsync();

        var saved = await client.PutAsJsonAsync("/jobs/worker-profile", new { headline = "Harvest worker", bio = "Ten seasons", district = "Bo", skills = new[] { "harvesting" }, password = "brand-new-pass", newPassword = "brand-new-pass" });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(email, Password));
        Assert.Equal(HttpStatusCode.BadRequest, await LoginAsync(email, "brand-new-pass"));
    }

    [Fact]
    public async Task Phone_number_and_nin_over_the_limit_are_rejected()
    {
        var (client, _) = await RegisterAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/jobs/worker-profile", Profile(phoneNumber: new string('1', 31)))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync("/jobs/worker-profile", Profile(nin: new string('1', 51)))).StatusCode);
    }

    [Fact]
    public async Task Email_cannot_be_changed_from_the_profile()
    {
        var (client, email) = await RegisterAsync();

        var saved = await client.PutAsJsonAsync("/jobs/worker-profile", new { headline = "Harvest worker", bio = "Ten seasons", district = "Bo", skills = new[] { "harvesting" }, email = NewEmail() });

        Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
        Assert.Equal(email, (await ReadAsync(saved))["email"]!.GetValue<string>());
        Assert.Equal(HttpStatusCode.OK, await LoginAsync(email, Password));
    }

    [Fact]
    public async Task Admins_cannot_apply_even_with_an_older_worker_profile()
    {
        var (user, token) = await factory.CreateUserAsync(UserRole.Seller);
        Assert.Equal(HttpStatusCode.OK, (await ClientFor(token).PutAsJsonAsync("/jobs/worker-profile", Profile())).StatusCode);

        // A seller later made an admin keeps their profile, and their new token carries the Admin role.
        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var promoted = await dbContext.Users.SingleAsync(account => account.Id == user.Id);
        promoted.ChangeRole(UserRole.Admin);
        await dbContext.SaveChangesAsync();
        var adminToken = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().GenerateToken(promoted);

        var poster = ClientFor((await factory.CreateUserAsync(UserRole.Admin)).Token);
        var created = await poster.PostAsJsonAsync("/jobs", new { title = "Harvesters", description = "Work", district = "Bo", workersNeeded = 2 });
        var jobId = (await ReadAsync(created))["id"]!.GetValue<Guid>();

        var applied = await ClientFor(adminToken).PostAsJsonAsync($"/jobs/{jobId}/apply", new { });

        Assert.Equal(HttpStatusCode.Forbidden, applied.StatusCode);
    }

    [Fact]
    public async Task A_rejected_save_changes_neither_the_account_nor_the_profile()
    {
        var (client, email) = await RegisterAsync();
        await client.PutAsJsonAsync("/jobs/worker-profile", Profile(headline: "First headline"));

        var blankHeadline = await client.PutAsJsonAsync("/jobs/worker-profile", Profile(phoneNumber: "+23277999999", nin: "NIN999", headline: " "));
        Assert.Equal(HttpStatusCode.BadRequest, blankHeadline.StatusCode);

        var profile = JsonNode.Parse(await client.GetStringAsync("/jobs/worker-profile"))!;
        Assert.Equal("First headline", profile["headline"]!.GetValue<string>());
        Assert.Equal(Phone, profile["phoneNumber"]!.GetValue<string>());
        Assert.Equal(email, profile["email"]!.GetValue<string>());
        Assert.Equal(Nin, profile["nin"]!.GetValue<string>());
    }

    [Fact]
    public async Task Applicants_list_includes_the_workers_email_but_not_their_nin()
    {
        var (client, email) = await RegisterAsync();
        await client.PutAsJsonAsync("/jobs/worker-profile", Profile());
        var admin = ClientFor((await factory.CreateUserAsync(UserRole.Admin)).Token);
        var created = await admin.PostAsJsonAsync("/jobs", new { title = "Harvesters", description = "Work", district = "Bo", workersNeeded = 2 });
        var jobId = (await ReadAsync(created))["id"]!.GetValue<Guid>();

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsJsonAsync($"/jobs/{jobId}/apply", new { })).StatusCode);

        var body = await admin.GetStringAsync($"/jobs/{jobId}/applicants");
        var applicant = Assert.Single(JsonNode.Parse(body)!.AsArray())!;
        Assert.Equal(email, applicant["email"]!.GetValue<string>());
        Assert.DoesNotContain(Nin, body);
    }
}
