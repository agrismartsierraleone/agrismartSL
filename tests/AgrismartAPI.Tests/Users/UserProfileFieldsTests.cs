using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Audit;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Persistence;
using AgrismartAPI.Tests.Fertilizer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgrismartAPI.Tests.Users;

/// <summary>Same in-memory API as the fertilizer tests, but with email delivery captured so nothing is really sent.</summary>
public class UserProfileApiFactory : FertilizerApiFactory
{
    public CapturingEmailSender Emails { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureTestServices(services => services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails)));
    }
}

public class UserProfileFieldsTests(UserProfileApiFactory factory) : IClassFixture<UserProfileApiFactory>
{
    private const string Phone = "+23276123456";
    private const string Nin = "SL-1234567";

    private static string NewEmail() => $"{Guid.NewGuid():N}@test.local";

    private static object RegisterBody(string? phoneNumber, string? nin, string? email = null) => new
    {
        name = "Test Buyer",
        email = email ?? NewEmail(),
        password = "Password123!",
        role = "buyer",
        phoneNumber,
        nin
    };

    private static object CreateBody(string role, string? phoneNumber, string? nin) => new
    {
        name = $"Test {role}",
        email = NewEmail(),
        password = "Password123!",
        role,
        phoneNumber,
        nin
    };

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.CreateUserTokenAsync(UserRole.Admin));
        return client;
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Fact]
    public async Task Register_stores_trimmed_phone_number_and_nin()
    {
        var email = NewEmail();

        var response = await factory.CreateClient().PostAsJsonAsync("/auth/register", RegisterBody($"  {Phone} ", $" {Nin} ", email));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var scope = factory.Services.CreateScope();
        var user = await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.SingleAsync(u => u.Email == email);
        Assert.Equal(Phone, user.PhoneNumber);
        Assert.Equal(Nin, user.Nin);
    }

    [Theory]
    [InlineData(null, Nin, "Phone number is required.")]
    [InlineData("   ", Nin, "Phone number is required.")]
    [InlineData(Phone, null, "NIN is required.")]
    [InlineData(Phone, "   ", "NIN is required.")]
    public async Task Register_rejects_a_missing_or_blank_field(string? phoneNumber, string? nin, string expectedMessage)
    {
        var email = NewEmail();

        var response = await factory.CreateClient().PostAsJsonAsync("/auth/register", RegisterBody(phoneNumber, nin, email));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(expectedMessage, (await ReadJsonAsync(response))["message"]!.GetValue<string>());

        using var scope = factory.Services.CreateScope();
        Assert.False(await scope.ServiceProvider.GetRequiredService<AppDbContext>().Users.AnyAsync(u => u.Email == email));
    }

    [Fact]
    public async Task Register_rejects_an_overlong_value()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/auth/register", RegisterBody(new string('1', 31), Nin));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData("buyer")]
    [InlineData("seller")]
    public async Task Create_user_requires_both_fields_for_buyers_and_sellers(string role)
    {
        var client = await AdminClientAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/users", CreateBody(role, null, Nin))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/users", CreateBody(role, Phone, null))).StatusCode);

        var response = await client.PostAsJsonAsync("/users", CreateBody(role, Phone, Nin));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(Phone, body["phoneNumber"]!.GetValue<string>());
        Assert.Equal(Nin, body["nin"]!.GetValue<string>());
    }

    [Fact]
    public async Task Create_user_does_not_require_the_fields_for_admins()
    {
        var client = await AdminClientAsync();

        var response = await client.PostAsJsonAsync("/users", CreateBody("admin", null, null));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Update_backfills_an_existing_user_and_keeps_the_values_out_of_audit_and_email()
    {
        var client = await AdminClientAsync();
        var email = NewEmail();
        Guid userId;
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var legacyUser = User.Register("Legacy Buyer", email, "not-a-real-hash", UserRole.Buyer);
            dbContext.Users.Add(legacyUser);
            await dbContext.SaveChangesAsync();
            userId = legacyUser.Id;
        }

        var response = await client.PutAsJsonAsync($"/users/{userId}/credentials", new { phoneNumber = Phone, nin = Nin });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await ReadJsonAsync(response);
        Assert.Equal(Phone, body["phoneNumber"]!.GetValue<string>());
        Assert.Equal(Nin, body["nin"]!.GetValue<string>());

        var sentEmail = Assert.Single(factory.Emails.Sent, sent => sent.To == email);
        Assert.Contains("NIN was updated.", sentEmail.Body);
        Assert.DoesNotContain(Nin, sentEmail.Body);
        Assert.DoesNotContain(Phone, sentEmail.Body);

        using (var scope = factory.Services.CreateScope())
        {
            var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
                .Set<AuditLog>().SingleAsync(entry => entry.TargetUserId == userId);
            Assert.DoesNotContain(Nin, audit.Details!);
            Assert.DoesNotContain(Phone, audit.Details!);
        }
    }

    [Fact]
    public async Task Update_leaves_the_fields_alone_when_they_are_omitted()
    {
        var client = await AdminClientAsync();
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/users", CreateBody("buyer", Phone, Nin)));

        var response = await client.PutAsJsonAsync($"/users/{created["id"]}/credentials", new { name = "New Name" });

        var body = await ReadJsonAsync(response);
        Assert.Equal(Phone, body["phoneNumber"]!.GetValue<string>());
        Assert.Equal(Nin, body["nin"]!.GetValue<string>());
    }
}

public class CapturingEmailSender : IEmailSender
{
    private readonly List<(string To, string Body)> sent = [];

    public IReadOnlyList<(string To, string Body)> Sent
    {
        get
        {
            lock (sent)
            {
                return sent.ToList();
            }
        }
    }

    public Task SendAsync(string toEmail, string toName, string subject, string htmlContent, CancellationToken cancellationToken = default)
    {
        lock (sent)
        {
            sent.Add((toEmail, htmlContent));
        }

        return Task.CompletedTask;
    }
}
