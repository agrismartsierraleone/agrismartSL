using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Domain.Audit;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgrismartAPI.Tests.Users;

public class ProfileUpdateEmailTests(UserProfileApiFactory factory) : IClassFixture<UserProfileApiFactory>
{
    private const string Password = "Password123!";
    private const string UpdateNotice = "The following changes were made";

    private static string NewEmail() => $"{Guid.NewGuid():N}@test.local";

    private async Task<HttpClient> AdminClientAsync()
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", await factory.CreateUserTokenAsync(UserRole.Admin));
        return client;
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    private List<string> UpdateNoticesTo(string email) =>
        factory.Emails.Sent.Where(sent => sent.To == email && sent.Body.Contains(UpdateNotice)).Select(sent => sent.Body).ToList();

    private static async Task<JsonNode> CreateUserAsync(HttpClient admin, string role, string email)
    {
        var response = await admin.PostAsJsonAsync("/users", new
        {
            name = $"Test {role}",
            email,
            password = Password,
            role,
            phoneNumber = "+23276123456",
            nin = "SL-1234567"
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<string> SellerIdForAsync(HttpClient client, JsonNode user)
    {
        var sellers = await ReadJsonAsync(await client.GetAsync("/sellers"));
        return sellers.AsArray()
            .Single(seller => seller!["userId"]?.GetValue<string>() == user["id"]!.GetValue<string>())!["id"]!
            .GetValue<string>();
    }

    [Theory]
    [InlineData("buyer")]
    [InlineData("seller")]
    public async Task Admin_updating_a_user_emails_that_user(string role)
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var user = await CreateUserAsync(admin, role, email);

        var response = await admin.PutAsJsonAsync($"/users/{user["id"]}/credentials", new { name = "Renamed" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Name changed to", Assert.Single(UpdateNoticesTo(email)));
    }

    [Fact]
    public async Task Changing_a_users_email_notifies_both_addresses()
    {
        var admin = await AdminClientAsync();
        var oldEmail = NewEmail();
        var newEmail = NewEmail();
        var user = await CreateUserAsync(admin, "buyer", oldEmail);

        await admin.PutAsJsonAsync($"/users/{user["id"]}/credentials", new { email = newEmail });

        Assert.Single(UpdateNoticesTo(oldEmail));
        Assert.Single(UpdateNoticesTo(newEmail));
    }

    [Fact]
    public async Task Admin_updating_a_seller_profile_emails_the_linked_account()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var user = await CreateUserAsync(admin, "seller", email);
        var sellerId = await SellerIdForAsync(admin, user);

        var response = await admin.PutAsJsonAsync($"/sellers/{sellerId}", new { name = "Test seller", contactPhone = "+23299000000", location = "Bo" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var notice = Assert.Single(UpdateNoticesTo(email));
        Assert.Contains("Contact phone changed to", notice);
        Assert.Contains("Location changed to", notice);
        Assert.Contains("Contact email was removed.", notice);
    }

    [Fact]
    public async Task Seller_updating_their_own_profile_is_also_emailed()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var user = await CreateUserAsync(admin, "seller", email);
        var sellerId = await SellerIdForAsync(admin, user);

        var login = await ReadJsonAsync(await factory.CreateClient().PostAsJsonAsync("/auth/login", new { email, password = Password }));
        var seller = factory.CreateClient();
        seller.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login["token"]!.GetValue<string>());

        var response = await seller.PutAsJsonAsync($"/sellers/{sellerId}", new { name = "New shop name", contactEmail = email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(UpdateNoticesTo(email));
    }

    [Fact]
    public async Task Saving_a_seller_profile_with_no_changes_sends_no_email()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var user = await CreateUserAsync(admin, "seller", email);
        var sellerId = await SellerIdForAsync(admin, user);

        var response = await admin.PutAsJsonAsync($"/sellers/{sellerId}", new { name = "Test seller", contactEmail = email });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(UpdateNoticesTo(email));
    }

    [Fact]
    public async Task Seller_profile_without_an_account_notifies_its_old_and_new_contact_email()
    {
        var admin = await AdminClientAsync();
        var oldContact = NewEmail();
        var newContact = NewEmail();
        var created = await ReadJsonAsync(await admin.PostAsJsonAsync("/sellers", new { name = "Walk-in farm", contactEmail = oldContact }));

        var response = await admin.PutAsJsonAsync($"/sellers/{created["id"]}", new { name = "Walk-in farm", contactEmail = newContact });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(UpdateNoticesTo(oldContact));
        Assert.Single(UpdateNoticesTo(newContact));
    }

    [Fact]
    public async Task Seller_profile_with_nowhere_to_send_still_updates()
    {
        var admin = await AdminClientAsync();
        var created = await ReadJsonAsync(await admin.PostAsJsonAsync("/sellers", new { name = "No contact farm" }));

        var response = await admin.PutAsJsonAsync($"/sellers/{created["id"]}", new { name = "Renamed farm" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Password_reset_emails_the_password_to_the_user_but_not_to_the_audit_log()
    {
        const string newPassword = "Fresh-Temp-Pass-1";
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var user = await CreateUserAsync(admin, "seller", email);

        var response = await admin.PutAsJsonAsync($"/users/{user["id"]}/credentials", new { newPassword });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(newPassword, Assert.Single(UpdateNoticesTo(email)));

        var userId = user["id"]!.GetValue<Guid>();
        using var scope = factory.Services.CreateScope();
        var audit = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Set<AuditLog>().Where(entry => entry.TargetUserId == userId).ToListAsync();
        Assert.Contains(audit, entry => entry.Action == "UserCredentialsUpdated");
        Assert.All(audit, entry => Assert.DoesNotContain(newPassword, entry.Details ?? string.Empty));
    }

    [Fact]
    public async Task Password_reset_with_an_email_change_keeps_the_password_away_from_the_old_address()
    {
        const string newPassword = "Fresh-Temp-Pass-2";
        var admin = await AdminClientAsync();
        var oldEmail = NewEmail();
        var newEmail = NewEmail();
        var user = await CreateUserAsync(admin, "buyer", oldEmail);

        await admin.PutAsJsonAsync($"/users/{user["id"]}/credentials", new { email = newEmail, newPassword });

        Assert.DoesNotContain(newPassword, Assert.Single(UpdateNoticesTo(oldEmail)));
        Assert.Contains(newPassword, Assert.Single(UpdateNoticesTo(newEmail)));
    }

    [Fact]
    public async Task Deleting_a_seller_profile_emails_the_linked_account()
    {
        var admin = await AdminClientAsync();
        var email = NewEmail();
        var user = await CreateUserAsync(admin, "seller", email);
        var sellerId = await SellerIdForAsync(admin, user);

        var response = await admin.DeleteAsync($"/sellers/{sellerId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(factory.Emails.Sent, sent => sent.To == email && sent.Body.Contains("has been deleted from AgrismartAPI"));
    }

    [Fact]
    public async Task Deleting_a_seller_profile_without_an_account_emails_its_contact_address()
    {
        var admin = await AdminClientAsync();
        var contact = NewEmail();
        var created = await ReadJsonAsync(await admin.PostAsJsonAsync("/sellers", new { name = "Walk-in farm", contactEmail = contact }));

        var response = await admin.DeleteAsync($"/sellers/{created["id"]}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Single(factory.Emails.Sent, sent => sent.To == contact && sent.Body.Contains("has been deleted from AgrismartAPI"));
    }
}
