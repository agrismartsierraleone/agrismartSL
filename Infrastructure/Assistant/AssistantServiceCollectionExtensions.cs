using System.Security.Claims;
using System.Threading.RateLimiting;
using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.AiTools;
using AgrismartAPI.Application.Assistant;
using AgrismartAPI.Application.Logistics;
using AgrismartAPI.Domain.Assistant;
using AgrismartAPI.Infrastructure.Persistence.Repositories;
using Anthropic;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Infrastructure.Assistant;

public static class AssistantServiceCollectionExtensions
{
    /// <summary>
    /// Registers the AI assistant. The "assistant" rate-limit policy is always registered (the controller
    /// references it); everything else only when "Features:AiAssistant" is true.
    /// </summary>
    public static IServiceCollection AddAssistant(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AssistantOptions>(configuration.GetSection(AssistantOptions.SectionName));
        services.Configure<DeliveryOptions>(configuration.GetSection(DeliveryOptions.SectionName));
        AddAssistantRateLimiting(services, configuration);

        if (!configuration.GetValue<bool>(AssistantOptions.FeatureFlagKey))
        {
            return services;
        }

        services.AddScoped<IFarmRepository, FarmRepository>();
        services.AddScoped<IConversationStore, ConversationStore>();
        services.AddScoped<AssistantTools>();
        services.AddScoped<AssistantService>();
        services.AddScoped<AiToolsService>();

        services.AddSingleton<IChatClient>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<AssistantOptions>>().Value;
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            return CreateProviderClient(options, loggerFactory.CreateLogger(typeof(AssistantServiceCollectionExtensions)))
                .AsBuilder()
                .UseFunctionInvocation(loggerFactory, invoker => invoker.MaximumIterationsPerRequest = options.MaxToolIterations)
                .Build(serviceProvider);
        });

        return services;
    }

    /// <summary>The only provider-specific code. Swap providers by adding a case that returns another IChatClient.</summary>
    private static IChatClient CreateProviderClient(AssistantOptions options, ILogger logger)
    {
        if (!string.Equals(options.Provider, "Anthropic", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Assistant provider '{Provider}' is not supported; the assistant will return 503.", options.Provider);
            return new UnconfiguredChatClient($"Unsupported assistant provider '{options.Provider}'.");
        }

        // Assistant:ApiKey comes from user-secrets or the Assistant__ApiKey environment variable;
        // ANTHROPIC_API_KEY is accepted as a fallback.
        var apiKey = !string.IsNullOrWhiteSpace(options.ApiKey)
            ? options.ApiKey
            : Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            logger.LogError("Assistant:ApiKey is not configured; the assistant will return 503.");
            return new UnconfiguredChatClient("Assistant:ApiKey is not configured.");
        }

        return new AnthropicClient { ApiKey = apiKey }.AsIChatClient(options.Model, options.MaxOutputTokens);
    }

    private static void AddAssistantRateLimiting(IServiceCollection services, IConfiguration configuration)
    {
        var dailyLimit = configuration.GetValue<int?>($"{AssistantOptions.SectionName}:{nameof(AssistantOptions.DailyRequestLimit)}")
                         ?? new AssistantOptions().DailyRequestLimit;

        services.AddRateLimiter(options =>
        {
            // No global limiter: only endpoints marked [EnableRateLimiting("assistant")] are affected.
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, cancellationToken) => new ValueTask(context.HttpContext.Response.WriteAsJsonAsync(
                new { message = "You have reached today's limit for the assistant. Please try again tomorrow." }, cancellationToken));

            options.AddPolicy(AssistantOptions.RateLimitPolicy, httpContext =>
            {
                // Per farmer; unauthenticated calls (rejected by [Authorize] anyway) share a per-IP bucket.
                var partitionKey = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                                   ?? $"ip:{httpContext.Connection.RemoteIpAddress}";

                return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = dailyLimit,
                    Window = TimeSpan.FromDays(1),
                    QueueLimit = 0
                });
            });
        });
    }
}
