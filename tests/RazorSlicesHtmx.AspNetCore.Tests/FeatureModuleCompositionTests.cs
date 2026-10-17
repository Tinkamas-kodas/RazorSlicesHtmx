using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Extensions;
using RazorSlicesHtmx.AspNetCore.Features;
using RazorSlicesHtmx.AspNetCore.Models;
using RazorSlicesHtmx.AspNetCore.Rendering;

namespace RazorSlicesHtmx.AspNetCore.Tests;

/// <summary>
/// Composition surface for large features: <see cref="BaseFeatureModule.MapSubgroup"/> nests a route
/// group under the feature's own prefix, and the public <c>ModuleResultFacade</c> lets a feature hand
/// its result facade to top-level collaborator classes instead of nesting them in the module type.
/// </summary>
public class FeatureModuleCompositionTests
{
    private static readonly HttpClient Http = new();

    private static WebApplicationBuilder CreateBuilder()
    {
        var builder = WebApplication.CreateBuilder([]);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.AddRazorSlicesHtmx();
        builder.Services.AddSingleton<IFeaturePageRenderer, StubFeaturePageRenderer>();
        builder.Services.AddSingleton<ITransientUiRenderer, StubTransientUiRenderer>();
        builder.Services.AddFeature<ComposedFeature>("/composed");
        return builder;
    }

    private static string BaseAddress(WebApplication app)
    {
        var addresses = app.Services.GetRequiredService<IServer>().Features
            .Get<IServerAddressesFeature>()!.Addresses;
        return addresses.First().TrimEnd(';');
    }

    [Fact]
    public async Task MapSubgroup_nests_section_routes_under_the_feature_prefix()
    {
        var app = CreateBuilder().Build();
        app.Services.GetRequiredService<FeatureRegistry>().MapEndpoints(app);

        await app.StartAsync();
        try
        {
            var baseUrl = BaseAddress(app);

            // The section on the feature group and the section on the subgroup both resolve;
            // the subgroup route is NOT reachable under the bare feature prefix.
            Assert.Equal(HttpStatusCode.OK, (await Http.GetAsync($"{baseUrl}/composed/list")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Http.GetAsync($"{baseUrl}/composed/events/click")).StatusCode);
            Assert.Equal(HttpStatusCode.NotFound, (await Http.GetAsync($"{baseUrl}/composed/click")).StatusCode);
        }
        finally
        {
            await app.StopAsync();
        }
    }

    [Fact]
    public void TopLevelSection_can_hold_the_module_result_facade()
    {
        var app = CreateBuilder().Build();
        app.Services.GetRequiredService<FeatureRegistry>().MapEndpoints(app);

        Assert.True(ComposedFeature.FacadeShared,
            "A non-nested section class should be able to accept the Result facade.");
    }
}

/// <summary>
/// Feature composed of two top-level sections: one maps <c>list</c> on the feature group, the other
/// maps <c>click</c> inside an <c>events</c> subgroup. Records whether the facade handed to a section
/// is non-null so the collaborator surface is observable from a test.
/// </summary>
public sealed class ComposedFeature(Results.FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public static bool FacadeShared { get; private set; }

    public override IReadOnlyList<NavigationItem> NavigationItems =>
    [
        new("composed", "Composed", "list")
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        var listSection = new ProbeSection(Result, "list");
        FacadeShared = !ReferenceEquals(listSection.Facade, null);
        listSection.Map(group);

        new ProbeSection(Result, "click").Map(MapSubgroup(group, "events"));
    }
}

/// <summary>
/// A route section living outside the module type, proving the facade is consumable from a collaborator.
/// </summary>
public sealed class ProbeSection(BaseFeatureModule.ModuleResultFacade facade, string route)
{
    public BaseFeatureModule.ModuleResultFacade Facade => facade;

    public void Map(RouteGroupBuilder group) => group.MapGet(route, () => Microsoft.AspNetCore.Http.Results.Ok());
}

/// <summary>
/// The app-owned page renderer the library does not provide; a stub keeps the host validatable.
/// </summary>
public sealed class StubFeaturePageRenderer : IFeaturePageRenderer
{
    public IResult RenderPage(FeatureShellContext context) => Microsoft.AspNetCore.Http.Results.Ok();

    public RazorSlice RenderNavigation(FeatureShellContext context) => throw new NotImplementedException();
}

/// <summary>
/// The adapter-owned transient UI renderer (toasts/dialogs); a stub keeps the host validatable without
/// referencing the Bootstrap5 adapter.
/// </summary>
public sealed class StubTransientUiRenderer : ITransientUiRenderer
{
    public RazorSlice RenderDialog(RazorSlice content) => content;

    public RazorSlice RenderToast(ToastModel toast) => throw new NotImplementedException();
}
