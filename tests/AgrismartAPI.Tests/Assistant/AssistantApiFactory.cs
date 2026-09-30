using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Orders;
using AgrismartAPI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgrismartAPI.Tests.Assistant;

/// <summary>
/// Hosts the real API with EF InMemory (never the configured database), synthetic weather, and the AI
/// provider replaced by <see cref="FakeChatClient"/> wrapped in the same function-invocation pipeline.
/// </summary>
public class AssistantApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"assistant-tests-{Guid.NewGuid()}";

    public FakeChatClient ChatClient { get; } = new();

    protected virtual bool AssistantEnabled => true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("OpenMeteo:UseSyntheticProvider", "true");
        builder.UseSetting("Features:AiAssistant", AssistantEnabled ? "true" : "false");
        builder.UseSetting("Assistant:DailyRequestLimit", "1000");

        builder.ConfigureServices(services =>
        {
            var efRegistrations = services
                .Where(descriptor =>
                    descriptor.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    descriptor.ServiceType == typeof(DbContextOptions) ||
                    descriptor.ServiceType == typeof(IDbContextOptionsConfiguration<AppDbContext>) ||
                    descriptor.ImplementationType == typeof(OrderHoldExpiryService))
                .ToList();
            foreach (var descriptor in efRegistrations)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(databaseName));

            if (AssistantEnabled)
            {
                services.RemoveAll<IChatClient>();
                services.AddSingleton<IChatClient>(serviceProvider =>
                    ChatClient.AsBuilder().UseFunctionInvocation().Build(serviceProvider));
            }
        });
    }

    /// <summary>Creates a user directly in the database and returns their id and a JWT.</summary>
    public async Task<(Guid UserId, string Token)> CreateUserAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Register("Test farmer", $"{Guid.NewGuid():N}@test.local", "not-a-real-hash", UserRole.Buyer);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return (user.Id, scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user));
    }

    public async Task SeedAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        dbContext.AddRange(entities);
        await dbContext.SaveChangesAsync();
    }
}

public class DisabledAssistantApiFactory : AssistantApiFactory
{
    protected override bool AssistantEnabled => false;
}
