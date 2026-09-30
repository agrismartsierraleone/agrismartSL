using AgrismartAPI.Domain.Common;

namespace AgrismartAPI.Domain.Assistant;

/// <summary>One AI assistant chat thread, owned by the farmer who started it.</summary>
public class AssistantConversation : Entity
{
    public Guid UserId { get; private init; }
    public DateTimeOffset CreatedAt { get; private init; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private AssistantConversation(Guid userId)
    {
        UserId = userId;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    // EF Core
    private AssistantConversation()
    {
    }

    public static AssistantConversation Start(Guid userId)
    {
        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        return new AssistantConversation(userId);
    }

    public void Touch() => UpdatedAt = DateTimeOffset.UtcNow;
}
