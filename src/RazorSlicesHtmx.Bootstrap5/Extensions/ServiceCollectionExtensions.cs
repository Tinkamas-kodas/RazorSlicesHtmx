using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RazorSlicesHtmx.AspNetCore.Options;
using RazorSlicesHtmx.Bootstrap5.Rendering;

namespace RazorSlicesHtmx.Bootstrap5.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddRazorSlicesHtmxBootstrap5(
        this IServiceCollection services,
        Action<RazorSlicesHtmxOptions>? configure = null)
    {
        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.AddSingleton<IPostConfigureOptions<RazorSlicesHtmxOptions>, Bootstrap5DefaultOptionsSetup>();

        services.AddSingleton<RazorSlicesHtmx.AspNetCore.Rendering.ITransientUiRenderer, Bootstrap5TransientUiRenderer>();
        return services;
    }

    private sealed class Bootstrap5DefaultOptionsSetup : IPostConfigureOptions<RazorSlicesHtmxOptions>
    {
        public void PostConfigure(string? name, RazorSlicesHtmxOptions options)
        {
            if (string.IsNullOrWhiteSpace(options.DialogClass))
            {
                options.DialogClass = "modal-dialog modal-dialog-centered";
            }

            if (options.ToastDelayMilliseconds is null)
            {
                options.ToastDelayMilliseconds = 2600;
            }
        }
    }
}