using System.Security.Claims;
using Apskaita.Feature.Account;
using Apskaita.Web.Layout;
using Apskaita.Web.Layout.Models;
using Apskaita.Web.Layout.Slices;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;
using RazorSlicesHtmx.AspNetCore.Options;

namespace Apskaita.Tests.Slices;

/// <summary>
/// Renders <c>_SideNav</c> to a string and checks that the sprite <c>&lt;use&gt;</c> hrefs are produced by
/// the slice's local <c>ResolveIconUrl</c> function. A Razor parse failure would emit the expression text
/// literally instead of the resolved URL, so the assertions cover both the resolved value and the absence
/// of the literal <c>@</c> text.
/// </summary>
public sealed class SideNavSliceTests
{
    private static readonly IServiceProvider Services = BuildServices();

    private static IServiceProvider BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IFeatureUri, StubFeatureUri>();
        services.AddSingleton(Microsoft.Extensions.Options.Options.Create(new RazorSlicesHtmxOptions()));
        return services.BuildServiceProvider();
    }

    private static async Task<string> RenderAsync(RazorSlice slice, HttpContext? httpContext = null)
    {
        slice.HttpContext = httpContext ?? new DefaultHttpContext();
        slice.HttpContext.RequestServices = Services;
        return await slice.RenderAsync();
    }

    private static HttpContext Authenticated()
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(AuthConstants.DisplayNameClaim, "Test User"),
            new Claim(ClaimTypes.Name, "user@example.com"),
        ],
        "TestAuth",
        ClaimTypes.Name,
        ClaimTypes.Role);

        return new DefaultHttpContext { User = new ClaimsPrincipal(identity) };
    }

    private static AppLayoutModel Model() => new(
        "Accounting",
        "clients",
        [
            new AdminNavItem("MAIN", "clients", "Clients", "/admin/clients/list"),
            new AdminNavItem("ADMIN", "invoice-templates", "Invoice Templates", "/admin/invoice-templates/list"),
        ]);

    private sealed class StubFeatureUri : IFeatureUri
    {
        public string Base => string.Empty;
        public string Uri(string segment) => segment;
    }

    [Fact]
    public async Task SideNav_ResolvesNavIconUrlsFromLocalFunction()
    {
        var html = await RenderAsync(_SideNav.Create(Model()));

        Assert.Contains($"href=\"/icons/sprite.svg?v={Assets.Version}#nav-clients\"", html);
        Assert.Contains($"href=\"/icons/sprite.svg?v={Assets.Version}#nav-invoice-templates\"", html);
        Assert.DoesNotContain("@ResolveIconUrl", html);
        Assert.DoesNotContain("sprite.svg@", html);
    }

    [Fact]
    public async Task SideNav_ResolvesSignoutIconAndLabel()
    {
        var html = await RenderAsync(_SideNav.Create(Model()), Authenticated());

        Assert.Contains($"href=\"/icons/sprite.svg?v={Assets.Version}#nav-signout\"", html);
        Assert.Contains("Log out", html);
        Assert.Contains("Test User", html);
        Assert.Contains("hx-post=\"/account/logout\"", html);
        Assert.DoesNotContain("@signoutUrl", html);
    }

    [Fact]
    public async Task SideNav_UserNameOpensSettings_WithSettingsIcon()
    {
        var html = await RenderAsync(_SideNav.Create(Model()), Authenticated());

        Assert.Contains($"href=\"/icons/sprite.svg?v={Assets.Version}#nav-settings\"", html);
        Assert.Contains("href=\"/account/me\"", html);
        Assert.Contains("Test User", html);
    }

    [Fact]
    public async Task SideNav_HidesSignoutForAnonymous()
    {
        var html = await RenderAsync(_SideNav.Create(Model()));

        Assert.DoesNotContain("nav-signout", html);
        Assert.DoesNotContain("Log out", html);
    }
}
