using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Assistant;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Application.Assistant;

/// <summary>Thrown when the AI provider fails; the controller turns it into a 503.</summary>
public class AssistantUnavailableException(string message, Exception innerException) : Exception(message, innerException);

public class AssistantService(
    IChatClient chatClient,
    AssistantTools tools,
    IConversationStore conversationStore,
    ICurrentUserService currentUser,
    IOptions<AssistantOptions> options,
    ILogger<AssistantService> logger)
{
    private const string FallbackReply =
        "Sorry, I could not answer that. Please ask your question in another way, or contact your local agricultural extension officer.";

    public async Task<AssistantChatResponse> ChatAsync(AssistantChatRequest request, CancellationToken cancellationToken = default)
    {
        var message = Validate(request);
        var userId = currentUser.UserId;
        var settings = options.Value;

        AssistantConversation conversation;
        var isNew = request.ConversationId is null;
        List<ChatMessage> history = [];

        if (request.ConversationId is { } conversationId)
        {
            conversation = await conversationStore.GetOwnedConversationAsync(conversationId, userId, cancellationToken)
                           ?? throw new NotFoundException($"Conversation '{conversationId}' was not found.");

            var stored = await conversationStore.GetRecentMessagesAsync(conversation.Id, settings.MaxHistoryMessages, cancellationToken);
            history.AddRange(stored
                .SkipWhile(m => m.Role != AssistantMessageRole.User) // trimmed history must start with the farmer's turn
                .Select(m => new ChatMessage(m.Role == AssistantMessageRole.User ? ChatRole.User : ChatRole.Assistant, m.Content)));
        }
        else
        {
            conversation = AssistantConversation.Start(userId);
        }

        var askedAt = DateTimeOffset.UtcNow;
        List<ChatMessage> messages =
        [
            new(ChatRole.System, AssistantPrompts.WithToday(DateOnly.FromDateTime(askedAt.UtcDateTime))),
            .. history,
            new(ChatRole.User, message)
        ];

        var chatOptions = new ChatOptions
        {
            Tools = tools.CreateTools(),
            ToolMode = ChatToolMode.Auto,
            MaxOutputTokens = settings.MaxOutputTokens
        };

        ChatResponse response;
        try
        {
            response = await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Assistant AI call failed for user {UserId}, conversation {ConversationId}", userId, conversation.Id);
            throw new AssistantUnavailableException("The AI provider call failed.", exception);
        }

        var usage = response.Usage;
        logger.LogInformation(
            "Assistant reply for user {UserId}, conversation {ConversationId}: model {Model}, input tokens {InputTokens}, output tokens {OutputTokens}, total tokens {TotalTokens}, finish reason {FinishReason}",
            userId, conversation.Id, response.ModelId ?? settings.Model, usage?.InputTokenCount, usage?.OutputTokenCount, usage?.TotalTokenCount,
            response.FinishReason?.Value);

        var reply = response.Text?.Trim();
        if (string.IsNullOrEmpty(reply))
        {
            logger.LogWarning("Assistant returned no text (finish reason {FinishReason}) for conversation {ConversationId}",
                response.FinishReason?.Value, conversation.Id);
            reply = FallbackReply;
        }

        // Nothing is persisted until the model has answered, so a failed call leaves no half-written turn behind.
        var userMessage = AssistantMessage.FromUser(conversation.Id, message, askedAt);
        var assistantMessage = AssistantMessage.FromAssistant(
            conversation.Id, reply, response.ModelId ?? settings.Model, usage?.InputTokenCount, usage?.OutputTokenCount, DateTimeOffset.UtcNow);
        conversation.Touch();

        await conversationStore.SaveTurnAsync(conversation, isNew, userMessage, assistantMessage, cancellationToken);

        return new AssistantChatResponse(conversation.Id, assistantMessage.Id, assistantMessage.Content);
    }

    public async Task ReportAsync(AssistantReportRequest request, CancellationToken cancellationToken = default)
    {
        var reason = request.Reason?.Trim();
        if (string.IsNullOrEmpty(reason))
        {
            throw Invalid("reason", "reason is required.");
        }

        if (reason.Length > AssistantMessageReport.MaxReasonLength)
        {
            throw Invalid("reason", $"reason must be at most {AssistantMessageReport.MaxReasonLength} characters.");
        }

        var userId = currentUser.UserId;
        var conversation = await conversationStore.GetOwnedConversationAsync(request.ConversationId, userId, cancellationToken);
        var message = conversation is null
            ? null
            : await conversationStore.GetMessageAsync(conversation.Id, request.MessageId, cancellationToken);

        // Only the owner's assistant replies can be reported; anything else is 404 so ids are not leaked.
        if (message is null || message.Role != AssistantMessageRole.Assistant)
        {
            throw new NotFoundException($"Message '{request.MessageId}' was not found in conversation '{request.ConversationId}'.");
        }

        await conversationStore.AddReportAsync(
            AssistantMessageReport.Create(message.Id, request.ConversationId, userId, reason), cancellationToken);

        logger.LogWarning("Assistant message {MessageId} in conversation {ConversationId} reported by user {UserId}",
            message.Id, request.ConversationId, userId);
    }

    private static string Validate(AssistantChatRequest request)
    {
        var message = request.Message?.Trim();
        if (string.IsNullOrEmpty(message))
        {
            throw Invalid("message", "message is required.");
        }

        if (message.Length > AssistantOptions.MaxMessageLength)
        {
            throw Invalid("message", $"message must be at most {AssistantOptions.MaxMessageLength} characters.");
        }

        return message;
    }

    private static ValidationException Invalid(string field, string error) =>
        new(error, new Dictionary<string, string[]> { [field] = [error] });
}
