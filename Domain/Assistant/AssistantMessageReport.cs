using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Assistant;

/// <summary>A farmer flagging an AI-generated reply as wrong or inappropriate (Google Play AI-content policy).</summary>
public class AssistantMessageReport : Entity
{
    public const int MaxReasonLength = 1000;

    public Guid MessageId { get; private init; }
    public Guid ConversationId { get; private init; }
    public Guid UserId { get; private init; }
    public string Reason { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }

    private AssistantMessageReport(Guid messageId, Guid conversationId, Guid userId, string reason)
    {
        MessageId = messageId;
        ConversationId = conversationId;
        UserId = userId;
        Reason = reason;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    // EF Core
    private AssistantMessageReport()
    {
        Reason = null!;
    }

    public static AssistantMessageReport Create(Guid messageId, Guid conversationId, Guid userId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reason is required.", nameof(reason));
        }

        return new AssistantMessageReport(messageId, conversationId, userId, reason.Trim());
    }
}
