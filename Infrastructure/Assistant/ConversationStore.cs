using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgrismartAPI.Infrastructure.Assistant;

public class ConversationStore(AppDbContext dbContext) : IConversationStore
{
    public Task<AssistantConversation?> GetOwnedConversationAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<AssistantConversation>()
            .SingleOrDefaultAsync(conversation => conversation.Id == conversationId && conversation.UserId == userId, cancellationToken);
    }

    public async Task<IReadOnlyList<AssistantMessage>> GetRecentMessagesAsync(Guid conversationId, int take, CancellationToken cancellationToken = default)
    {
        var newestFirst = await dbContext.Set<AssistantMessage>()
            .AsNoTracking()
            .Where(message => message.ConversationId == conversationId)
            .OrderByDescending(message => message.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        newestFirst.Reverse();
        return newestFirst;
    }

    public Task<AssistantMessage?> GetMessageAsync(Guid conversationId, Guid messageId, CancellationToken cancellationToken = default)
    {
        return dbContext.Set<AssistantMessage>()
            .AsNoTracking()
            .SingleOrDefaultAsync(message => message.Id == messageId && message.ConversationId == conversationId, cancellationToken);
    }

    public async Task SaveTurnAsync(
        AssistantConversation conversation,
        bool isNewConversation,
        AssistantMessage userMessage,
        AssistantMessage assistantMessage,
        CancellationToken cancellationToken = default)
    {
        if (isNewConversation)
        {
            dbContext.Set<AssistantConversation>().Add(conversation);
        }

        dbContext.Set<AssistantMessage>().AddRange(userMessage, assistantMessage);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddReportAsync(AssistantMessageReport report, CancellationToken cancellationToken = default)
    {
        dbContext.Set<AssistantMessageReport>().Add(report);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
