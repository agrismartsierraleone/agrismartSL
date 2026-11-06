using AgrismartAPI.Application.Abstractions;
using AgrismartAPI.Application.Common;
using AgrismartAPI.Application.Fertilizer;
using AgrismartAPI.Domain.Audit;
using AgrismartAPI.Domain.Carts;
using AgrismartAPI.Domain.Checkout;
using AgrismartAPI.Domain.Farms;
using AgrismartAPI.Domain.Fertilizer;
using AgrismartAPI.Domain.Harvest;
using AgrismartAPI.Domain.Market;
using AgrismartAPI.Domain.Orders;
using AgrismartAPI.Domain.Scans;
using AgrismartAPI.Domain.Tutorials;
using AgrismartAPI.Domain.Users;
using AgrismartAPI.Infrastructure.Auth;
using AgrismartAPI.Infrastructure.Diagnosis;
using AgrismartAPI.Infrastructure.Diagnosis.Kindwise;
using AgrismartAPI.Infrastructure.Email;
using AgrismartAPI.Infrastructure.Harvest;
using AgrismartAPI.Infrastructure.Orders;
using AgrismartAPI.Infrastructure.Payments.Monime;
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
        services.Configure<MonimeSettings>(configuration.GetSection(MonimeSettings.SectionName));
        services.Configure<OpenMeteoOptions>(configuration.GetSection(OpenMeteoOptions.SectionName));
        services.Configure<FertilizerCalculatorOptions>(configuration.GetSection(FertilizerCalculatorOptions.SectionName));

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
        services.AddScoped<ICheckoutSessionRepository, CheckoutSessionRepository>();
        services.AddScoped<ICheckoutAuditLogRepository, CheckoutAuditLogRepository>();
        services.AddScoped<ICropRepository, CropRepository>();
        services.AddScoped<IHarvestPredictionRepository, HarvestPredictionRepository>();
        services.AddScoped<IFertilizerCropRepository, FertilizerCropRepository>();
        services.AddScoped<IFertilizerProductRepository, FertilizerProductRepository>();
        services.AddScoped<IFertilizerRecommendationRepository, FertilizerRecommendationRepository>();
        services.AddScoped<IMyFarmRepository, MyFarmRepository>();
        services.AddScoped<AgrismartAPI.Domain.Jobs.IJobRepository, JobRepository>();
        services.AddScoped<AgrismartAPI.Domain.Market.IMarketPriceRepository, MarketPriceRepository>();
        services.AddSingleton<IMarketPricePdfRenderer, AgrismartAPI.Infrastructure.Market.MarketPricePdfRenderer>();

        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        // Emails are sent inline, so keep this short or a SendGrid outage holds up the API response.
        services.AddHttpClient<IEmailSender, SendGridEmailSender>(client => client.Timeout = TimeSpan.FromSeconds(10));

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

        services.AddHttpClient<IMonimeCheckoutClient, MonimeCheckoutClient>();
        services.AddHostedService<OrderHoldExpiryService>();

        // Deterministic synthetic climate when no outbound network should be assumed (local dev,
        // CI); real Open-Meteo archive/forecast data otherwise. See Infrastructure/Harvest.
        var offlineWeather =
            configuration.GetValue<bool>($"{OpenMeteoOptions.SectionName}:UseSyntheticProvider") ||
            string.Equals(Environment.GetEnvironmentVariable("AGRISMART_OFFLINE"), "true", StringComparison.OrdinalIgnoreCase);

        if (offlineWeather)
        {
            services.AddSingleton<IWeatherProvider, SyntheticWeatherProvider>();
        }
        else
        {
            services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(client =>
            {
                client.Timeout = TimeSpan.FromSeconds(20);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("AgrismartAPI-Harvest/1.0");
            });
        }

        services.AddScoped<IHarvestPredictor, GddHarvestPredictor>();

        return services;
    }
}
