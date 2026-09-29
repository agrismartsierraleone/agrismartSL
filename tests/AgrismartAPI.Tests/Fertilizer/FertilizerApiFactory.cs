using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Orders;
using AgrismartAPI.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace AgrismartAPI.Tests.Fertilizer;

/// <summary>
/// Hosts the real API in memory, with the Postgres DbContext swapped for EF InMemory (so tests never
/// touch the configured database) and the order-expiry background job removed.
/// </summary>
public class FertilizerApiFactory : WebApplicationFactory<Program>
{
    private readonly string databaseName = $"fertilizer-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("OpenMeteo:UseSyntheticProvider", "true");

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
        });
    }

    /// <summary>Creates a user directly in the database and returns a JWT for them.</summary>
    public async Task<string> CreateUserTokenAsync(UserRole role = UserRole.Buyer)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Register($"Test {role}", $"{Guid.NewGuid():N}@test.local", "not-a-real-hash", role);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
    }
}
