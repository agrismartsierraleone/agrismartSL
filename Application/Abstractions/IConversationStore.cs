using AgrismartAPI.Domain.Assistant;

namespace AgrismartAPI.Application.Abstractions;

/// <summary>Persistence for AI assistant conversations, messages and content reports.</summary>
public interface IConversationStore
{
    /// <summary>The conversation if it exists and belongs to <paramref name="userId"/>; otherwise null.</summary>
    Task<AssistantConversation?> GetOwnedConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>The newest <paramref name="take"/> messages, returned oldest first.</summary>
    Task<IReadOnlyList<AssistantMessage>> GetRecentMessagesAsync(Guid conversationId, int take, CancellationToken cancellationToken = default);

    Task<AssistantMessage?> GetMessageAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken = default);

    /// <summary>Saves a new or existing conversation together with the new turn in one transaction.</summary>
    Task SaveTurnAsync(
        AssistantConversation conversation,
        bool isNewConversation,
        AssistantMessage userMessage,
        AssistantMessage assistantMessage,
        CancellationToken cancellationToken = default);

    Task AddReportAsync(AssistantMessageReport report, CancellationToken cancellationToken = default);
}
