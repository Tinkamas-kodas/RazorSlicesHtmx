using System.Security.Claims;
using Apskaita.Feature.Account.Persistence;
using Apskaita.Feature.Account.Models;
using Apskaita.Feature.Account.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Calendar;
using Apskaita.Shared.Routing;
using Microsoft.AspNetCore.Authentication;

namespace Apskaita.Feature.Account.Endpoints;

/// <summary>
/// Account feature: login page, provider challenges, cookie sign-out, the pending-access landing page for
/// roleless users, and the account page with calendar-connector management (connect/disconnect). Policies
/// are per-endpoint on purpose: login/challenge are anonymous (CON-004), logout/pending/denied need a
/// session but no role, and list/connect/disconnect require <see cref="AuthPolicies.Operational"/>.
/// </summary>
public sealed class AccountEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("MAIN", "account", "Account", "me", Order: 2, AuthorizationPolicy: AuthPolicies.Operational)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("login", () => HtmxFragmentResult.Create(_LoginPage.Create()).Build())
            .AllowAnonymous();

        group.MapGet("challenge/{scheme}", async (string scheme, string? returnUrl, HttpContext ctx, IAuthenticationSchemeProvider schemes, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Apskaita.Auth.Challenge");

            if (scheme != AuthConstants.EntraScheme && scheme != AuthConstants.GoogleScheme)
            {
                logger.LogWarning("challenge rejected: unknown provider scheme '{Scheme}'", scheme);
                return Results.Problem("Provider must be 'entra' or 'google'.", statusCode: StatusCodes.Status400BadRequest, title: "Unknown provider");
            }

            // The provider is only registered when its credentials are configured.
            if (await schemes.GetSchemeAsync(scheme) is null)
            {
                logger.LogWarning("challenge rejected: provider '{Scheme}' is not configured (Auth:{Scheme}:ClientId is empty)", scheme, char.ToUpperInvariant(scheme[0]) + scheme[1..]);
                return Results.Problem($"Provider '{scheme}' has no ClientId configured in the Auth section.", statusCode: StatusCodes.Status400BadRequest, title: "Provider not configured");
            }

            // Open-redirect protection (SEC-002): only local paths may be used as the post-login target.
            var target = IsLocalUrl(returnUrl) ? returnUrl! : "/";
            logger.LogInformation("challenging provider '{Scheme}' with returnUrl='{Target}'", scheme, target);
            var props = new AuthenticationProperties { RedirectUri = target };
            return Results.Challenge(properties: props, authenticationSchemes: [scheme]);
        }).AllowAnonymous();

        group.MapPost("logout", async (HttpContext ctx) =>
        {
            await ctx.SignOutAsync(AuthConstants.CookieScheme);

            // htmx sign-out (side nav button): full-page redirect via header instead of a swapped response.
            if (string.Equals(ctx.Request.Headers["HX-Request"], "true", StringComparison.OrdinalIgnoreCase))
            {
                ctx.Response.Headers["HX-Redirect"] = "/account/login";
                return Results.Ok();
            }

            return Results.Redirect("/account/login");
        });

        group.MapGet("pending", (HttpContext ctx) =>
        {
            var displayName = ctx.User.FindFirst(AuthConstants.DisplayNameClaim)?.Value ?? "you";
            var email = ctx.User.FindFirst(ClaimTypes.Name)?.Value ?? string.Empty;
            return HtmxFragmentResult.Create(_PendingPage.Create(new PendingPageModel(displayName, email))).Build();
        });

        group.MapGet("denied", () => HtmxFragmentResult.Create(_DeniedPage.Create()).Build());

        // --- Account page + calendar connectors (formerly the Settings feature) ---

        // The feature base comes from IFeatureUri (the registered route group), so these URLs follow the
        // prefix this module is mounted under — no hardcoded "/account". The query string is preserved so
        // the connect round-trip notices (?connected / ?connectError) survive the root redirect.
        group.MapGet("", (IFeatureUri uri, HttpContext ctx) =>
            Results.Redirect(uri.Uri("me") + ctx.Request.QueryString.Value, permanent: false))
            .RequireAuthorization(AuthPolicies.Operational);

        group.MapGet("me", async (HttpContext ctx, IFeatureUri uri, CalendarConnectorRegistry registry, CalendarConnectorStore store, IOptions<ConnectorAuthOptions> options, CancellationToken ct) =>
        {
            var model = await CreatePageModelAsync(ctx, uri, registry, store, options, ct);
            return Result.Page(_AccountPage.Create(model));
        }).RequireAuthorization(AuthPolicies.Operational);

        group.MapGet("connect/{provider}", async (string provider, IFeatureUri uri, CalendarConnectorRegistry registry, IAuthenticationSchemeProvider schemes, ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("Apskaita.Account.Connect");

            var found = registry.Find(provider);
            if (found is null || !found.IsImplemented)
            {
                logger.LogWarning("connect rejected: unknown or unimplemented provider '{Provider}'", provider);
                return Results.Problem("Provider must be a known calendar provider.", statusCode: StatusCodes.Status400BadRequest, title: "Unknown provider");
            }

            // The consent scheme is only registered when connector credentials are configured.
            if (await schemes.GetSchemeAsync(AuthConstants.ConnectEntraScheme) is null)
            {
                logger.LogWarning("connect rejected: scheme '{Scheme}' is not configured (Auth:Connectors:ClientId is empty)", AuthConstants.ConnectEntraScheme);
                return Results.Problem("Microsoft calendar sign-in is not configured on this server.", statusCode: StatusCodes.Status400BadRequest, title: "Provider not configured");
            }

            var props = new AuthenticationProperties { RedirectUri = $"{uri.Uri("me")}?connected={found.Key}" };
            logger.LogInformation("challenging consent scheme '{Scheme}' for provider '{Provider}'", AuthConstants.ConnectEntraScheme, found.Key);
            return Results.Challenge(properties: props, authenticationSchemes: [AuthConstants.ConnectEntraScheme]);
        }).RequireAuthorization(AuthPolicies.Operational);

        group.MapPost("disconnect/{id:guid}", async (Guid id, CalendarConnectorRegistry registry, CalendarConnectorStore store, CancellationToken ct) =>
        {
            var provider = await store.DisconnectByIdAsync(id, ct);
            if (provider is null)
            {
                return Results.NotFound();
            }

            var cards = await BuildCardsAsync(registry, store, ct);

            return Result.Fragment(_Empty.Create())
                .WithOob("#connectors-table", _ConnectorsTable.Create(cards))
                .WithToast("Calendar account was disconnected.", title: "Account", tone: ToastTone.Info);
        }).RequireAuthorization(AuthPolicies.Operational);
    }

    private static async Task<AccountPageModel> CreatePageModelAsync(HttpContext ctx, IFeatureUri uri, CalendarConnectorRegistry registry, CalendarConnectorStore store, IOptions<ConnectorAuthOptions> options, CancellationToken ct)
    {
        var cards = await BuildCardsAsync(registry, store, ct);

        var connected = ctx.Request.Query["connected"].ToString();
        var connectError = ctx.Request.Query["connectError"].ToString();

        string? notice = null;
        var noticeKind = "success";
        string? adminConsentUrl = null;

        if (connectError.Equals("admin-consent", StringComparison.OrdinalIgnoreCase))
        {
            notice = "Your organization requires administrator approval for this connection. " +
                "Ask an admin of that organization to approve the connector app, then connect again.";
            noticeKind = "danger";
            adminConsentUrl = $"https://login.microsoftonline.com/common/adminconsent?client_id={Uri.EscapeDataString(options.Value.ClientId)}" +
                $"&redirect_uri={Uri.EscapeDataString($"{ctx.Request.Scheme}://{ctx.Request.Host}{uri.Base}")}";
        }
        else if (!string.IsNullOrEmpty(connectError))
        {
            notice = "The calendar connection could not be completed. Please try again.";
            noticeKind = "danger";
        }
        else if (!string.IsNullOrEmpty(connected))
        {
            var card = cards.FirstOrDefault(c => string.Equals(c.Key, connected, StringComparison.OrdinalIgnoreCase));
            notice = card?.Connections.Count > 0
                ? $"{card.Label} was connected."
                : "The calendar connection could not be completed. Please try again.";
            noticeKind = card?.Connections.Count > 0 ? "success" : "danger";
        }

        return new AccountPageModel(notice, noticeKind, cards, adminConsentUrl);
    }

    private static async Task<List<CalendarConnectorCardModel>> BuildCardsAsync(CalendarConnectorRegistry registry, CalendarConnectorStore store, CancellationToken ct)
    {
        var connectors = await store.GetOwnAllAsync(ct);

        return registry.All.Select(provider =>
        {
            var providerConnectors = provider.Provider is null
                ? []
                : connectors.Where(c => c.Provider == provider.Provider).ToArray();

            return new CalendarConnectorCardModel(
                provider.Key,
                provider.Label,
                provider.IsImplemented,
                providerConnectors
                    .OrderBy(c => c.AccountEmail, StringComparer.OrdinalIgnoreCase)
                    .Select(c => new CalendarConnectionModel(c.Id, c.AccountEmail, c.DisplayName, c.ConnectedAtUtc, c.LastSyncAtUtc, c.LastSyncError))
                    .ToList());
        }).ToList();
    }

    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        url[0] == '/' &&
        (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) &&
        !url.Contains("://");
}
