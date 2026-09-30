namespace AgrismartAPI.Application.Assistant;

/// <summary>
/// "Assistant" configuration section. <see cref="ApiKey"/> must come from user-secrets or the
/// Assistant__ApiKey environment variable, never from a committed appsettings file.
/// </summary>
public class AssistantOptions
{
    public const string SectionName = "Assistant";

    /// <summary>Feature flag key: "Features": { "AiAssistant": true }.</summary>
    public const string FeatureFlagKey = "Features:AiAssistant";

    /// <summary>Rate-limit policy applied to the assistant endpoints only.</summary>
    public const string RateLimitPolicy = "assistant";

    public const int MaxMessageLength = 1000;

    /// <summary>Only "Anthropic" is implemented; the rest of the feature only depends on IChatClient.</summary>
    public string Provider { get; set; } = "Anthropic";

    public string Model { get; set; } = "claude-opus-5-5";

    public string? ApiKey { get; set; }

    /// <summary>Cap on the model's output per request, including its internal reasoning.</summary>
    public int MaxOutputTokens { get; set; } = 8000;

    /// <summary>How many stored messages of a conversation are sent back to the model.</summary>
    public int MaxHistoryMessages { get; set; } = 20;

    /// <summary>Upper bound on tool-call round-trips per question.</summary>
    public int MaxToolIterations { get; set; } = 6;

    /// <summary>Chat/report requests allowed per farmer per day.</summary>
    public int DailyRequestLimit { get; set; } = 30;
}
