using System.Security.Claims;
using Apskaita.Feature.Account.Persistence;
using Apskaita.Feature.Account.Models;
using Apskaita.Feature.Account.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Calendar;
using Apskaita.Shared.Routing;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

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
        group.MapGet("login", (IOptions<AuthOptions> options) =>
        {
            // Offer only providers whose credentials are configured, so an unconfigured provider never
            // renders a button that would 400 on challenge.
            var model = new LoginModel(
                !string.IsNullOrWhiteSpace(options.Value.Entra.ClientId),
                !string.IsNullOrWhiteSpace(options.Value.Google.ClientId));
            return HtmxFragmentResult.Create(_LoginPage.Create(model)).Build();
        })
            .AllowAnonymous();

        group.MapGet("challenge/{scheme}", async (string scheme, string? returnUrl, IAuthenticationSchemeProvider schemes, ILoggerFactory loggerFactory) =>
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
                var configKey = char.ToUpperInvariant(scheme[0]) + scheme[1..];
                logger.LogWarning("challenge rejected: provider '{Scheme}' is not configured (Auth:{ConfigKey}:ClientId is empty)", scheme, configKey);
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

        group.MapGet("me", async (HttpContext ctx, IFeatureUri uri, CalendarConnectorRegistry registry, CalendarConnectorStore store, IcsConnectorStore ics, IOptions<ConnectorAuthOptions> options, CancellationToken ct) =>
        {
            var model = await CreatePageModelAsync(ctx, uri, registry, store, ics, options, ct);
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

        // Connections dock fragment for the time-tracking page: one compact row per connection with a
        // health dot, no events and no statistics (those stay on /account/me). Loaded by its own hx-get
        // (outside the grid), so a grid refresh never re-fetches it.
        //
        // weekStart is required because an imported calendar's dot is week-relative: green only when its
        // stored events actually cover the displayed week.
        group.MapGet("connections", async (HttpContext ctx, IFeatureUri uri, string? weekStart, CalendarConnectorRegistry registry, CalendarConnectorStore store, IcsConnectorStore ics, CancellationToken ct) =>
        {
            var cards = await BuildCardsAsync(registry, store, ics, ct);

            var anchor = DateOnly.TryParse(weekStart, out var parsed)
                ? parsed
                : DateOnly.FromDateTime(DateTime.Now);

            var displayOffset = BrowserTimeZone.GetDisplayOffset(ctx);
            var weekStartUtc = BrowserTimeZone.WallToUtc(anchor.ToDateTime(TimeOnly.MinValue), displayOffset);
            var weekEndUtc = BrowserTimeZone.WallToUtc(anchor.AddDays(6).ToDateTime(new TimeOnly(23, 59, 59)), displayOffset);

            var model = ConnectionsDockModelFactory.Create(
                cards,
                anchor,
                weekStartUtc,
                weekEndUtc,
                connectionId => uri.Uri($"ics/{connectionId}/import-dialog"));

            return Result.Fragment(_ConnectionsDock.Create(model));
        }).RequireAuthorization(AuthPolicies.Operational);

        // The dock's "Import" link target: the file picker for one imported calendar. The dialog posts to
        // the existing import endpoint and marks the dock as the calling surface.
        group.MapGet("ics/{id:guid}/import-dialog", async (Guid id, IcsConnectorStore ics, CancellationToken ct) =>
        {
            var connector = await ics.GetOwnByIdAsync(id, ct);
            return connector is null
                ? Results.NotFound()
                : Result.ShowDialog(_IcsImportDialog.Create(new IcsImportDialogModel(id, connector.DisplayName)));
        }).RequireAuthorization(AuthPolicies.Operational);

        group.MapPost("disconnect/{id:guid}", async (Guid id, CalendarConnectorRegistry registry, CalendarConnectorStore store, IcsConnectorStore ics, CancellationToken ct) =>
        {
            var provider = await store.DisconnectByIdAsync(id, ct);
            if (provider is null)
            {
                return Results.NotFound();
            }

            // Disconnecting happens on the account page only — the dock shows status and the import
            // action, and links here for everything else.
            var cards = await BuildCardsAsync(registry, store, ics, ct);

            return Result.Fragment(_Empty.Create())
                .WithOob("#connectors-table", _ConnectorsTable.Create(cards))
                .WithToast("Calendar account was disconnected.", title: "Account", tone: ToastTone.Info);
        }).RequireAuthorization(AuthPolicies.Operational);

        // --- Imported (.ics) calendars: a named container the user fills by uploading an .ics export ---

        // Creates an empty named imported calendar. The name is also the account key of the event
        // references, so it is unique per user and never renamed (disconnect and recreate instead).
        group.MapPost("ics/create", async ([FromForm] string? name, IcsConnectorStore ics, CalendarConnectorRegistry registry, CalendarConnectorStore store, CancellationToken ct) =>
        {
            var (connector, error) = await ics.CreateOwnAsync(name, ct);
            if (connector is null)
            {
                return Result.Fragment(_Empty.Create())
                    .WithToast(error ?? "The imported calendar could not be created.", title: "Imported calendar", tone: ToastTone.Error);
            }

            var cards = await BuildCardsAsync(registry, store, ics, ct);

            return Result.Fragment(_Empty.Create())
                .WithOob("#connectors-table", _ConnectorsTable.Create(cards))
                .WithToast("Upload an .ics file to load its events.", title: $"\"{connector.DisplayName}\" created", tone: ToastTone.Success);
        }).RequireAuthorization(AuthPolicies.Operational);

        // Replaces the events of one imported calendar. The connector itself is never cleared — only its
        // stored event rows are swapped, in one transaction (SEC-005: the raw file is never persisted).
        group.MapPost("ics/{id:guid}/import", async (
            HttpContext ctx,
            Guid id,
            IFormFile? file,
            IcsConnectorStore ics,
            IcsCalendarImporter importer,
            CalendarConnectorRegistry registry,
            CalendarConnectorStore store,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var connector = await ics.GetOwnByIdAsync(id, ct);
            if (connector is null)
            {
                return Results.NotFound();
            }

            var logger = loggerFactory.CreateLogger("Apskaita.Calendar.Ics");

            // An import that produced no change: the dock's dialog stays open (re-rendered with the
            // message); the account page's inline form just toasts.
            IResult ImportFailed(string message) => CalledFromDock(ctx)
                ? Result.Fragment(_IcsImportDialog.Create(new IcsImportDialogModel(id, connector.DisplayName, message)))
                : Result.Fragment(_Empty.Create()).WithToast(message, title: connector.DisplayName, tone: ToastTone.Error);

            if (file is null || file.Length == 0)
            {
                return ImportFailed("Choose an .ics file to import.");
            }

            if (file.Length > IcsCalendarImporter.MaxUploadBytes)
            {
                return ImportFailed($"The file is larger than {IcsCalendarImporter.MaxUploadBytes / (1024 * 1024)} MB.");
            }

            IcsImportResult outcome;
            await using (var stream = file.OpenReadStream())
            {
                outcome = importer.Import(stream);
            }

            if (!outcome.Success)
            {
                logger.LogWarning("ics import rejected for calendar {Account}: {Error}", connector.AccountKey, outcome.Error);
                return ImportFailed(outcome.Error ?? "The calendar file could not be imported.");
            }

            await ics.ImportAsync(connector, outcome.Events, ct);

            var message = $"{outcome.Events.Count} event(s) imported." + (outcome.Warning is null ? string.Empty : " " + outcome.Warning);

            if (CalledFromDock(ctx))
            {
                return Result.Fragment(_Empty.Create())
                    .ClearDialog()
                    .WithTrigger("connections-refresh")
                    .WithToast(message, title: connector.DisplayName, tone: ToastTone.Success);
            }

            var cards = await BuildCardsAsync(registry, store, ics, ct);

            return Result.Fragment(_Empty.Create())
                .WithOob("#connectors-table", _ConnectorsTable.Create(cards))
                .WithToast(message, title: connector.DisplayName, tone: ToastTone.Success);
        }).RequireAuthorization(AuthPolicies.Operational);
    }

    /// <summary>
    /// True when the request originated from the time-tracking page (the connections dock). htmx always
    /// sends the browser URL, so the dock and the account page need no extra form field to be told apart —
    /// the dock refreshes itself by trigger, the account page by the table's OOB swap.
    /// </summary>
    private static bool CalledFromDock(HttpContext ctx) =>
        ctx.Request.Headers["HX-Current-URL"].ToString().Contains("/time-tracking", StringComparison.OrdinalIgnoreCase);

    private static async Task<AccountPageModel> CreatePageModelAsync(HttpContext ctx, IFeatureUri uri, CalendarConnectorRegistry registry, CalendarConnectorStore store, IcsConnectorStore ics, IOptions<ConnectorAuthOptions> options, CancellationToken ct)
    {
        var cards = await BuildCardsAsync(registry, store, ics, ct);

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

    private static async Task<List<CalendarConnectorCardModel>> BuildCardsAsync(CalendarConnectorRegistry registry, CalendarConnectorStore store, IcsConnectorStore ics, CancellationToken ct)
    {
        var connectors = await store.GetOwnAllAsync(ct);

        // Imported calendars carry statistics (event count + covered range); OAuth rows do not.
        var icsIds = connectors.Where(c => c.Provider == CalendarProvider.Ics).Select(c => c.Id).ToArray();
        var stats = await ics.GetStatsAsync(icsIds, ct);

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
                    .Select(c => new CalendarConnectionModel(
                        c.Id,
                        c.AccountEmail,
                        c.DisplayName,
                        c.ConnectedAtUtc,
                        c.LastSyncAtUtc,
                        c.LastSyncError,
                        c.Provider == CalendarProvider.Ics
                            ? stats.TryGetValue(c.Id, out var value) ? value : CalendarConnectionStats.Empty
                            : null))
                    .ToList());
        }).ToList();
    }

    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        url[0] == '/' &&
        (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) &&
        !url.Contains("://");
}
