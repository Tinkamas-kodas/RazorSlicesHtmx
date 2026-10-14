using Apskaita.Shared.Ui.Common.Models;
using Apskaita.Shared.Ui.Common.Slices;
using Apskaita.Web.Shared.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

namespace Apskaita.Tests.Slices;

/// <summary>
/// Slice render-to-string tests using the RazorSlices <c>RenderAsync()</c> approach. The admin
/// <c>_ViewImports</c> injects <see cref="IFeatureUri"/>, which RazorSlices initializes eagerly, so a
/// minimal service provider is attached via <see cref="RazorSlice.HttpContext"/> before rendering.
/// </summary>
public sealed class StatusSlicesTests
{
    private static readonly IServiceProvider Services = BuildServices();

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureUri, StubFeatureUri>();
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync(RazorSlice slice)
    {
        slice.HttpContext = new DefaultHttpContext { RequestServices = Services };
        return await slice.RenderAsync();
    }

    private static StatusToggleModel Model(bool isActive) =>
        new(Guid.Parse("0ff0d6b0-6f29-4db7-8af8-266909de00f4"), isActive, "/admin/clients/toggle/xyz");

    private sealed class StubFeatureUri : IFeatureUri
    {
        public string Base => string.Empty;
        public string Uri(string segment) => segment;
    }

    [Fact]
    public async Task StatusBadge_Active_RendersSuccessActive()
    {
        var html = await RenderAsync(_StatusBadge.Create(Model(true)));

        Assert.Contains("bg-success", html);
        Assert.Contains("Active", html);
        Assert.DoesNotContain("Inactive", html);
    }

    [Fact]
    public async Task StatusBadge_Inactive_RendersSecondaryInactive()
    {
        var html = await RenderAsync(_StatusBadge.Create(Model(false)));

        Assert.Contains("bg-secondary", html);
        Assert.Contains("Inactive", html);
        Assert.DoesNotContain(">Active<", html);
    }

    [Fact]
    public async Task StatusAction_Active_RendersDeactivateWithSelfTarget()
    {
        var html = await RenderAsync(_StatusAction.Create(Model(true)));

        Assert.Contains("btn-outline-warning", html);
        Assert.Contains("Deactivate", html);
        Assert.Contains("hx-post=\"/admin/clients/toggle/xyz\"", html);
        Assert.Contains("hx-target=\"#status-action-0ff0d6b0-6f29-4db7-8af8-266909de00f4\"", html);
        Assert.Contains("hx-params=\"none\"", html);
    }

    [Fact]
    public async Task StatusAction_Inactive_RendersActivate()
    {
        var html = await RenderAsync(_StatusAction.Create(Model(false)));

        Assert.Contains("btn-outline-success", html);
        Assert.Contains("Activate", html);
        Assert.DoesNotContain("Deactivate", html);
    }
}
