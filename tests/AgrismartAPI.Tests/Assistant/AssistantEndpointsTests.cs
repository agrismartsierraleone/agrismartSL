using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using AgrismartAPI.Domain.Assistant;
using Microsoft.Extensions.AI;

namespace AgrismartAPI.Tests.Assistant;

/// <summary>End-to-end over HTTP with the AI provider mocked by <see cref="FakeChatClient"/>.</summary>
public class AssistantEndpointsTests(AssistantApiFactory factory) : IClassFixture<AssistantApiFactory>
{
    private HttpClient ClientFor(string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<JsonNode> ReadJsonAsync(HttpResponseMessage response) =>
        JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

    [Fact]
    public async Task Status_is_anonymous_and_reports_enabled()
    {
        var response = await factory.CreateClient().GetAsync("/api/assistant/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await ReadJsonAsync(response))["enabled"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Chat_and_report_require_sign_in()
    {
        var client = factory.CreateClient();

        var chat = await client.PostAsJsonAsync("/api/assistant/chat", new { message = "Hello" });
        var report = await client.PostAsJsonAsync("/api/assistant/report",
            new { conversationId = Guid.NewGuid(), messageId = Guid.NewGuid(), reason = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, chat.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, report.StatusCode);
    }

    [Fact]
    public async Task Chat_calls_tools_for_the_signed_in_farmer_and_continues_the_conversation()
    {
        var (farmerId, token) = await factory.CreateUserAsync();
        var farm = Farm.Create(farmerId, "Hill farm", "Bombali", 8.9, -12.0, 1.5);
        farm.AddCrop("rice-upland", new DateOnly(2026, 6, 1), 1.0);
        await factory.SeedAsync(farm);

        factory.ChatClient.Script = FakeChatClient.CallToolThenAnswer(
            "get_farm_profile",
            new Dictionary<string, object?>(),
            toolJson => toolJson.Contains("Hill farm") ? "Your Hill farm has upland rice." : "No farm found.");
        var client = ClientFor(token);

        var first = await client.PostAsJsonAsync("/api/assistant/chat", new { conversationId = (Guid?)null, message = "What do I grow?" });

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var body = await ReadJsonAsync(first);
        Assert.Equal("Your Hill farm has upland rice.", body["reply"]!.GetValue<string>());
        var conversationId = body["conversationId"]!.GetValue<Guid>();
        Assert.NotEqual(Guid.Empty, body["messageId"]!.GetValue<Guid>());

        factory.ChatClient.Script = (_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Harvest when the grains are hard."));
        var second = await client.PostAsJsonAsync("/api/assistant/chat", new { conversationId, message = "When do I harvest?" });

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal(conversationId, (await ReadJsonAsync(second))["conversationId"]!.GetValue<Guid>());

        // The model saw the system prompt, the previous turn from storage, then the new question.
        var lastCall = factory.ChatClient.Calls[^1];
        Assert.Equal(ChatRole.System, lastCall[0].Role);
        Assert.Contains("Sierra Leone", lastCall[0].Text);
        Assert.Equal(["What do I grow?", "Your Hill farm has upland rice.", "When do I harvest?"], lastCall.Skip(1).Select(m => m.Text));
    }

    [Fact]
    public async Task Chat_returns_503_when_the_ai_provider_fails()
    {
        var (_, token) = await factory.CreateUserAsync();
        factory.ChatClient.Script = (_, _) => throw new HttpRequestException("provider down");

        var response = await ClientFor(token).PostAsJsonAsync("/api/assistant/chat", new { message = "When should I plant rice?" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("The assistant is unavailable right now. Please try again later.",
            (await ReadJsonAsync(response))["message"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chat_validates_the_message()
    {
        var (_, token) = await factory.CreateUserAsync();
        var client = ClientFor(token);

        var empty = await client.PostAsJsonAsync("/api/assistant/chat", new { message = "  " });
        var tooLong = await client.PostAsJsonAsync("/api/assistant/chat", new { message = new string('a', 1001) });

        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, tooLong.StatusCode);
        Assert.NotNull((await ReadJsonAsync(tooLong))["errors"]!["message"]);
    }

    [Fact]
    public async Task Chat_cannot_continue_another_farmers_conversation()
    {
        var (_, ownerToken) = await factory.CreateUserAsync();
        var (_, otherToken) = await factory.CreateUserAsync();
        factory.ChatClient.Script = (_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Hello farmer."));
        var created = await ReadJsonAsync(await ClientFor(ownerToken).PostAsJsonAsync("/api/assistant/chat", new { message = "Hi" }));

        var response = await ClientFor(otherToken).PostAsJsonAsync("/api/assistant/chat",
            new { conversationId = created["conversationId"]!.GetValue<Guid>(), message = "Show me their farm" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Report_accepts_the_owners_reply_and_hides_others()
    {
        var (_, ownerToken) = await factory.CreateUserAsync();
        var (_, otherToken) = await factory.CreateUserAsync();
        factory.ChatClient.Script = (_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Use lots of chemicals."));
        var reply = await ReadJsonAsync(await ClientFor(ownerToken).PostAsJsonAsync("/api/assistant/chat", new { message = "Pests?" }));
        var report = new
        {
            conversationId = reply["conversationId"]!.GetValue<Guid>(),
            messageId = reply["messageId"]!.GetValue<Guid>(),
            reason = "Unsafe advice"
        };

        var own = await ClientFor(ownerToken).PostAsJsonAsync("/api/assistant/report", report);
        var others = await ClientFor(otherToken).PostAsJsonAsync("/api/assistant/report", report);
        var noReason = await ClientFor(ownerToken).PostAsJsonAsync("/api/assistant/report", report with { reason = "" });

        Assert.Equal(HttpStatusCode.NoContent, own.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, others.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
    }

    [Fact]
    public async Task Existing_endpoints_are_not_rate_limited_or_changed()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/crops");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class DisabledAssistantEndpointsTests(DisabledAssistantApiFactory factory) : IClassFixture<DisabledAssistantApiFactory>
{
    [Fact]
    public async Task Disabled_assistant_reports_false_and_hides_chat()
    {
        var (_, token) = await factory.CreateUserAsync();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var status = await client.GetAsync("/api/assistant/status");
        var chat = await client.PostAsJsonAsync("/api/assistant/chat", new { message = "Hello" });

        Assert.Equal(HttpStatusCode.OK, status.StatusCode);
        Assert.False(JsonNode.Parse(await status.Content.ReadAsStringAsync())!["enabled"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.NotFound, chat.StatusCode);
    }
}
