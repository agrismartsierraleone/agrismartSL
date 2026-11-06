using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Persistence;
using AgrismartAPI.Tests.Assistant;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace AgrismartAPI.Tests.AiTools;

/// <summary>The in-memory API with a fake AI provider, plus delivery rates and helpers for each role.</summary>
public class AiToolsApiFactory : AssistantApiFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("Delivery:BaseFee", "100");
        builder.UseSetting("Delivery:RatePerKm", "2");
        builder.UseSetting("Delivery:RatePerKg", "1");
    }

    public async Task<(User User, string Token)> CreateUserAsync(UserRole role, string? phoneNumber = null)
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = User.Register($"Test {role}", $"{Guid.NewGuid():N}@test.local", "not-a-real-hash", role, phoneNumber: phoneNumber);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return (user, scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user));
    }

    public async Task<(Seller Seller, User User, string Token)> CreateSellerAsync(string? location = null)
    {
        var (user, token) = await CreateUserAsync(UserRole.Seller);
        var seller = Seller.Create($"Seller {Guid.NewGuid():N}", user.Email, null, location, user.Id);
        await SeedAsync(seller);
        return (seller, user, token);
    }
}
