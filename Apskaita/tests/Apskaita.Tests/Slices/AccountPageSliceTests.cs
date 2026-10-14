using Apskaita.Feature.Account.Models;
using Apskaita.Feature.Account.Slices;
using Apskaita.Shared.Routing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using RazorSlices;

namespace Apskaita.Tests.Slices;

/// <summary>
/// Renders <c>_AccountPage</c> (and its <c>_ConnectorsTable</c> partial) to a string and checks the
/// provider row states: implemented-with-connections, implemented-with-no-connections, and the
/// "coming soon" placeholder, plus the connect round-trip notice and admin consent link.
/// </summary>
public sealed class AccountPageSliceTests
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

    private static CalendarConnectorCardModel Outlook(params CalendarConnectionModel[] connections) =>
        new("outlook365", "Outlook 365 / Microsoft 365", true, connections);

    private static CalendarConnectorCardModel Google() =>
        new("google", "Google Calendar", false, []);

    private static CalendarConnectionModel Connection(string email, string displayName) =>
        new(Guid.Parse("0f8e6f34-1c2d-4a5b-9c8d-7e6f5a4b3c2d"), email, displayName,
            new DateTime(2026, 10, 12, 8, 0, 0, DateTimeKind.Utc), null, null);

    private sealed class StubFeatureUri : IFeatureUri
    {
        public string Base => string.Empty;
        public string Uri(string segment) => $"/account/{segment.TrimStart('/')}";
    }

    [Fact]
    public async Task AccountPage_WithConnections_RendersEmailAndDisconnect()
    {
        var model = new AccountPageModel(null, "success", [
            Outlook(Connection("dovydas@nrdcompanies.com", "NRD User")),
            Google()
        ], null);

        var html = await RenderAsync(_AccountPage.Create(model));

        Assert.Contains("Outlook 365 / Microsoft 365", html);
        Assert.Contains("dovydas@nrdcompanies.com", html);
        Assert.Contains("NRD User", html);
        Assert.Contains("/account/disconnect/0f8e6f34-1c2d-4a5b-9c8d-7e6f5a4b3c2d", html);
        Assert.Contains("Coming soon", html);
        Assert.Contains("/account/connect/outlook365", html);
    }

    [Fact]
    public async Task AccountPage_WithoutConnections_RendersEmptyState()
    {
        var model = new AccountPageModel(null, "success", [Outlook()], null);

        var html = await RenderAsync(_AccountPage.Create(model));

        Assert.Contains("No accounts connected yet.", html);
        Assert.DoesNotContain("list-group-item", html);
    }

    [Fact]
    public async Task AccountPage_WithNotice_RendersAlertAndConsentLink()
    {
        var model = new AccountPageModel(
            "Your organization requires administrator approval for this connection.",
            "danger",
            [Outlook()],
            "https://login.microsoftonline.com/common/adminconsent?client_id=abc");

        var html = await RenderAsync(_AccountPage.Create(model));

        Assert.Contains("alert-danger", html);
        Assert.Contains("administrator approval", html);
        Assert.Contains("https://login.microsoftonline.com/common/adminconsent?client_id=abc", html);
    }

    [Fact]
    public async Task AccountPage_Table_DoesNotCarrySwapOob()
    {
        // Regression: the connectors table must NOT hardcode hx-swap-oob. When /account/me is returned as
        // an HTMX fragment, htmx scans the whole response for [hx-swap-oob]; a nested swap attribute would
        // detach the table from the article and drop it (no #connectors-table target on the current page),
        // leaving an empty content pane. The disconnect flow wraps the table in HtmxOob externally.
        var model = new AccountPageModel(null, "success", [Outlook(), Google()], null);

        var html = await RenderAsync(_AccountPage.Create(model));

        Assert.DoesNotContain("hx-swap-oob", html);
        Assert.Contains("id=\"connectors-table\"", html);
    }

    [Fact]    public async Task AccountPage_WithoutNotice_RendersNoAlert()
    {
        var model = new AccountPageModel(null, "success", [Outlook()], null);

        var html = await RenderAsync(_AccountPage.Create(model));

        Assert.DoesNotContain("alert-", html);
    }
}
