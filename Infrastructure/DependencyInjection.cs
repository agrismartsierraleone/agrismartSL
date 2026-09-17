using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Domain.Audit;
using AgrismartAPI.Domain.Carts;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Scans;
using AgrismartAPI.Domain.Tutorials;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Auth;
using AgrismartAPI.Infrastructure.Diagnosis;
using AgrismartAPI.Infrastructure.Diagnosis.Kindwise;
using AgrismartAPI.Infrastructure.Email;
using AgrismartAPI.Infrastructure.Persistence;
using AgrismartAPI.Infrastructure.Persistence.Repositories;
using AgrismartAPI.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgrismartAPI.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        services.Configure<KindwiseSettings>(configuration.GetSection(KindwiseSettings.SectionName));
        services.Configure<SendGridSettings>(configuration.GetSection(SendGridSettings.SectionName));
        services.Configure<AppLinksSettings>(configuration.GetSection(AppLinksSettings.SectionName));

        services.AddHttpContextAccessor();
        services.AddMemoryCache();

        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IBiometricCredentialRepository, BiometricCredentialRepository>();
        services.AddScoped<IScanHistoryRepository, ScanHistoryRepository>();
        services.AddScoped<IMarketRepository, MarketRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<ISellerRepository, SellerRepository>();
        services.AddScoped<ITutorialRepository, TutorialRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<ICartRepository, CartRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddHttpClient<IEmailSender, SendGridEmailSender>();

        // Real pest/disease detection when a Kindwise API key is configured; otherwise fall back to
        // canned samples so local dev keeps working without a kindwise.com account.
        services.AddScoped<MockDiagnosisEngine>();
        services.AddHttpClient<KindwiseCropHealthEngine>((sp, client) =>
        {
            var settings = sp.GetRequiredService<IOptions<KindwiseSettings>>().Value;
            client.BaseAddress = new Uri(settings.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds);
        });
        services.AddScoped<IDiagnosisEngine>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<KindwiseSettings>>().Value;
            return string.IsNullOrWhiteSpace(settings.ApiKey)
                ? sp.GetRequiredService<MockDiagnosisEngine>()
                : sp.GetRequiredService<KindwiseCropHealthEngine>();
        });

        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IBiometricChallengeStore, BiometricChallengeStore>();
        services.AddSingleton<IBiometricSignatureVerifier, BiometricSignatureVerifier>();

        return services;
    }
}
