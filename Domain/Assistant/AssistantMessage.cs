using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Assistant;

public enum AssistantMessageRole
{
    User,
    Assistant
}

/// <summary>
/// A single turn in an <see cref="AssistantConversation"/>. Only the visible text is stored; tool calls
/// are re-run on demand so stored history never goes stale against the farmer's latest data.
/// </summary>
public class AssistantMessage : Entity
{
    public const int MaxContentLength = 8000;

    public Guid ConversationId { get; private init; }
    public AssistantMessageRole Role { get; private init; }
    public string Content { get; private init; }

    /// <summary>Model id that produced an assistant message. Null for user messages.</summary>
    public string? Model { get; private init; }

    /// <summary>Token usage for the whole request that produced an assistant message (including tool round-trips).</summary>
    public long? InputTokens { get; private init; }
    public long? OutputTokens { get; private init; }

    public DateTimeOffset CreatedAt { get; private init; }

    private AssistantMessage(
        Guid conversationId, AssistantMessageRole role, string content, string? model, long? inputTokens, long? outputTokens, DateTimeOffset createdAt)
    {
        ConversationId = conversationId;
        Role = role;
        Content = content;
        Model = model;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        CreatedAt = createdAt;
    }

    // EF Core
    private AssistantMessage()
    {
        Content = null!;
    }

    public static AssistantMessage FromUser(Guid conversationId, string content, DateTimeOffset createdAt) =>
        new(conversationId, AssistantMessageRole.User, Require(content), null, null, null, createdAt);

    public static AssistantMessage FromAssistant(
        Guid conversationId, string content, string? model, long? inputTokens, long? outputTokens, DateTimeOffset createdAt) =>
        new(conversationId, AssistantMessageRole.Assistant, Require(content), model, inputTokens, outputTokens, createdAt);

    private static string Require(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Message content is required.", nameof(content));
        }

        return content.Length <= MaxContentLength ? content : content[..MaxContentLength];
    }
}
