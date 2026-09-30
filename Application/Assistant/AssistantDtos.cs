namespace AgrismartAPI.Application.Assistant;

/// <summary>A farmer's question.</summary>
/// <param name="ConversationId">Continue this conversation; omit or null to start a new one.</param>
/// <param name="Message">The question as text (required, at most 1000 characters). Voice is transcribed on the phone.</param>
/// <remarks>
/// Kept as a flat object so optional fields (e.g. an uploaded crop-photo id for pest/disease questions)
/// can be added later without breaking existing clients. Images are not supported yet.
/// </remarks>
public record AssistantChatRequest(Guid? ConversationId, string? Message);

/// <param name="ConversationId">Send this back to continue the conversation.</param>
/// <param name="MessageId">Id of the assistant's reply; use it to report the reply.</param>
/// <param name="Reply">The assistant's answer.</param>
public record AssistantChatResponse(Guid ConversationId, Guid MessageId, string Reply);

/// <summary>Flags an AI-generated reply as wrong, harmful or inappropriate.</summary>
public record AssistantReportRequest(Guid ConversationId, Guid MessageId, string? Reason);

public record AssistantStatusResponse(bool Enabled);
