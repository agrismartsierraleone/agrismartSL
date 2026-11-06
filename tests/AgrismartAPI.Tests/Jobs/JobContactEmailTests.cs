using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Tests.AiTools;
using Xunit;

namespace AgrismartAPI.Tests.Jobs;

public class JobContactEmailTests(AiToolsApiFactory factory) : IClassFixture<AiToolsApiFactory>
{
    private async Task<HttpResponseMessage> PostJobAsync(string? contactEmail)
    {
        var (_, token) = await factory.CreateUserAsync(UserRole.Admin);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.PostAsJsonAsync("/jobs", new { title = "Harvesters", description = "Work", district = "Bo", workersNeeded = 2, contactEmail });
    }

    [Fact]
    public async Task Contact_email_is_saved_and_returned_when_reading_the_job()
    {
        var created = await PostJobAsync("  jobs@example.com ");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var job = JsonNode.Parse(await created.Content.ReadAsStringAsync())!;
        Assert.Equal("jobs@example.com", job["contactEmail"]!.GetValue<string>());

        var (_, token) = await factory.CreateUserAsync(UserRole.Buyer);
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var read = JsonNode.Parse(await client.GetStringAsync($"/jobs/{job["id"]}"))!;
        Assert.Equal("jobs@example.com", read["contactEmail"]!.GetValue<string>());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    public async Task Contact_email_is_optional(string? contactEmail)
    {
        var created = await PostJobAsync(contactEmail);

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Null(JsonNode.Parse(await created.Content.ReadAsStringAsync())!["contactEmail"]);
    }

    [Theory]
    [InlineData("not-an-email")]
    [InlineData("two@@example.com")]
    [InlineData("Jobs Desk <jobs@example.com>")]
    public async Task Invalid_contact_email_is_rejected(string contactEmail)
    {
        var response = await PostJobAsync(contactEmail);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Contact_email_over_the_limit_is_rejected()
    {
        var response = await PostJobAsync($"{new string('a', 250)}@example.com");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
