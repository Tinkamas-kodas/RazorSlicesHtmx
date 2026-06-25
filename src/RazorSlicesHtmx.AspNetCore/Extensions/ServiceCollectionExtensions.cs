using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlicesHtmx.AspNetCore.Infrastructure;
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
        services.AddSingleton<FeatureResultBuilder>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        return services;
    }
}