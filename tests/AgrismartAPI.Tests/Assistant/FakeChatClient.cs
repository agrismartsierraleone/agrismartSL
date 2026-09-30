using Microsoft.Extensions.AI;

namespace AgrismartAPI.Tests.Assistant;

/// <summary>
/// Scripted stand-in for the AI provider. Each call receives the full message list (so tests can see the
/// system prompt, trimmed history and tool results) and returns whatever the script says.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly object gate = new();

    public Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> Script { get; set; } =
        (_, _) => new ChatResponse(new ChatMessage(ChatRole.Assistant, "Plant rice at the start of the rainy season."));

    public List<IReadOnlyList<ChatMessage>> Calls { get; } = [];

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var snapshot = messages.ToList();
        lock (gate)
        {
            Calls.Add(snapshot);
        }

        return Task.FromResult(Script(snapshot, options));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }

    /// <summary>First call asks for <paramref name="toolName"/>; the second call answers with <paramref name="answer"/> applied to the tool result JSON.</summary>
    public static Func<IReadOnlyList<ChatMessage>, ChatOptions?, ChatResponse> CallToolThenAnswer(
        string toolName, IDictionary<string, object?> arguments, Func<string, string> answer) =>
        (messages, _) =>
        {
            var toolResult = messages.SelectMany(m => m.Contents).OfType<FunctionResultContent>().LastOrDefault();
            if (toolResult is null)
            {
                return new ChatResponse(new ChatMessage(ChatRole.Assistant,
                    [new FunctionCallContent("call-1", toolName, arguments)]));
            }

            return new ChatResponse(new ChatMessage(ChatRole.Assistant, answer(toolResult.Result?.ToString() ?? "")))
            {
                Usage = new UsageDetails { InputTokenCount = 120, OutputTokenCount = 30, TotalTokenCount = 150 }
            };
        };
}
