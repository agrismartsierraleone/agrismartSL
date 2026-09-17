using AgrismartAPI.Application.Auth;
using AgrismartAPI.Application.Carts;
using AgrismartAPI.Application.Diagnoses;
using AgrismartAPI.Application.Market;
using AgrismartAPI.Application.Orders;
using AgrismartAPI.Application.Scans;
using AgrismartAPI.Application.Tutorials;
using AgrismartAPI.Application.Users;
using Microsoft.Extensions.DependencyInjection;

namespace AgrismartAPI.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<DiagnosisAppService>();
        services.AddScoped<ScanAppService>();
        services.AddScoped<MarketAppService>();
        services.AddScoped<CategoryAppService>();
        services.AddScoped<SellerAppService>();
        services.AddScoped<TutorialAppService>();
        services.AddScoped<AuthAppService>();
        services.AddScoped<OrderAppService>();
        services.AddScoped<UserAppService>();
        services.AddScoped<CartAppService>();

        return services;
    }
}
