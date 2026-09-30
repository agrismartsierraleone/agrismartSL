using Microsoft.Extensions.AI;

namespace AgrismartAPI.Infrastructure.Assistant;

/// <summary>
/// Stands in when no API key (or an unsupported provider) is configured, so the app still starts and
/// the assistant fails per request with a 503 instead of breaking dependency injection.
/// </summary>
internal sealed class UnconfiguredChatClient(string reason) : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(reason);

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException(reason);

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
