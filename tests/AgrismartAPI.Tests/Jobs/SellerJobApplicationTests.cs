using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Tests.AiTools;
using Xunit;

namespace AgrismartAPI.Tests.Jobs;

public class SellerJobApplicationTests(AiToolsApiFactory factory) : IClassFixture<AiToolsApiFactory>
{
    private HttpClient ClientFor(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    [Fact]
    public async Task Seller_can_browse_jobs_create_a_worker_profile_and_apply()
    {
        var admin = ClientFor((await factory.CreateUserAsync(UserRole.Admin)).Token);
        var (sellerUser, sellerToken) = await factory.CreateUserAsync(UserRole.Seller, phoneNumber: "+23276000000");
        var seller = ClientFor(sellerToken);
        var created = await admin.PostAsJsonAsync("/jobs", new { title = "Harvesters", description = "Work", district = "Bo", workersNeeded = 2 });
        var jobId = JsonNode.Parse(await created.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();

        var jobs = JsonNode.Parse(await seller.GetStringAsync("/jobs?district=Bo"))!.AsArray();
        Assert.Contains(jobs, job => job!["id"]!.GetValue<Guid>() == jobId);

        Assert.Equal(HttpStatusCode.NotFound, (await seller.GetAsync("/jobs/worker-profile")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await seller.PostAsJsonAsync($"/jobs/{jobId}/apply", new { message = "Hi" })).StatusCode);

        var profile = await seller.PutAsJsonAsync("/jobs/worker-profile", new
        {
            headline = "Harvest worker",
            bio = "Ten seasons",
            district = "Bo",
            skills = new[] { "harvesting" },
            isAvailable = true
        });
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);

        var applied = await seller.PostAsJsonAsync($"/jobs/{jobId}/apply", new { message = "I can start Monday" });
        Assert.Equal(HttpStatusCode.NoContent, applied.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await seller.PostAsJsonAsync($"/jobs/{jobId}/apply", new { })).StatusCode);

        var applicant = Assert.Single(JsonNode.Parse(await admin.GetStringAsync($"/jobs/{jobId}/applicants"))!.AsArray())!;
        Assert.Equal(sellerUser.Id, applicant["workerUserId"]!.GetValue<Guid>());
        Assert.Equal("+23276000000", applicant["phoneNumber"]!.GetValue<string>());
    }
}
