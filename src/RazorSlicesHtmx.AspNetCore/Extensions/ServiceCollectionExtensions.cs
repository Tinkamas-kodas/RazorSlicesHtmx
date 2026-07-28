using Microsoft.Extensions.DependencyInjection;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.AspNetCore.Results;

namespace RazorSlicesHtmx.AspNetCore.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRazorSlicesHtmx(
        this IServiceCollection services,
        Action<RazorSlicesHtmxOptions>? configure = null)
    {
        services.AddHttpContextAccessor();
        services.AddOptions<RazorSlicesHtmxOptions>();
        services.AddOptions<HtmxErrorOptions>();
        services.AddOptions<FeatureRegistrationOptions>();
        services.AddSingleton<FeatureResultBuilder>();
        services.AddSingleton<INavOobStrategy, HxCurrentUrlNavStrategy>();
        services.AddSingleton(sp => FeatureRegistry.FromRegistrations(sp));
        services.AddScoped<HtmxStateContext>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }

    public static IServiceCollection AddFeature<TFeature>(
        this IServiceCollection services,
        string? routeGroupPrefix = null)
        where TFeature : class, IFeatureModule
    {
        services.Configure<FeatureRegistrationOptions>(options =>
        {
            if (options.Registrations.Any(x => x.FeatureType == typeof(TFeature)))
            {
                throw new InvalidOperationException($"Feature '{typeof(TFeature).FullName}' is already registered.");
            }

            options.Registrations.Add(new FeatureRegistration(typeof(TFeature), routeGroupPrefix));
        });

        return services;
    }
}