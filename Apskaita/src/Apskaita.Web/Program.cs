using Apskaita.Web.Layout.Rendering;
using Apskaita.Web.Features.Account;
using Apskaita.Web.Features.Account.Authentication;
using Apskaita.Web.Features.Account.Endpoints;
using Apskaita.Web.Features.Account.Services;
using Apskaita.Web.Features.Admin.ClientsRegistry.Endpoints;
using Apskaita.Web.Features.Admin.IssuerRegistry.Endpoints;
using Apskaita.Web.Features.Admin.UnitRegistry.Endpoints;
using Apskaita.Web.Features.Admin.ProjectRegistry.Endpoints;
using Apskaita.Web.Features.Admin.InvoiceTemplateRegistry.Endpoints;
using Apskaita.Web.Features.Admin.SystemConfig.Endpoints;
using Apskaita.Web.Features.Admin.UserRegistry.Endpoints;
using Apskaita.Web.Shared.Calendar;
using FluentValidation;
using Apskaita.Entities;
using Apskaita.Web.Features.TimeTracking.Endpoints;
using Apskaita.Web.Features.TimeTracking.Validators;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.Routing;
using Apskaita.Web.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using AdminFeatureType=Apskaita.Web.Features.Admin.ClientsRegistry.Models.ClientListQuery;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorSlicesHtmx();

builder.Services.AddAntiforgery();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<IFeatureUri, HttpFeatureUri>();
builder.Services.AddRazorSlicesHtmxBootstrap5();
builder.Services.AddSingleton<IFeaturePageRenderer, AppFeaturePageRenderer>();
builder.Services.AddFeature<TimeTrackingEndpoints>("/time-tracking");
builder.Services.AddFeature<ClientsEndpoints>("/admin/clients");
builder.Services.AddFeature<IssuersEndpoints>("/admin/issuers");
builder.Services.AddFeature<UnitsEndpoints>("/admin/units");
builder.Services.AddFeature<ProjectsEndpoints>("/admin/projects");
builder.Services.AddFeature<InvoiceTemplatesEndpoints>("/admin/invoice-templates");
builder.Services.AddFeature<SystemSettingsEndpoints>("/admin/system-settings");
builder.Services.AddFeature<UsersEndpoints>("/admin/users");
builder.Services.AddFeature<AccountEndpoints>("/account");

// Calendar connectors (feature-calendar-connectors-1): provider registry + Outlook 365 Graph connector.
// Feature modules are singletons, so the registry holds singleton provider metadata only; the scoped
// connector (and the store it uses) is resolved per request inside endpoints.
builder.Services.AddSingleton<ConnectorTokenProtector>();
builder.Services.AddScoped<CalendarConnectorStore>();
builder.Services.AddScoped<Outlook365Connector>(sp => new Outlook365Connector(
    sp.GetRequiredService<CalendarConnectorStore>(),
    sp.GetRequiredService<ConnectorTokenProtector>(),
    sp.GetRequiredService<IHttpClientFactory>(),
    sp.GetRequiredService<IOptions<ConnectorAuthOptions>>(),
    sp.GetRequiredService<ILoggerFactory>().CreateLogger("Apskaita.Calendar.Outlook365")));
builder.Services.AddSingleton<ICalendarProviderConnector>(new CalendarProviderInfo(Outlook365Connector.ProviderKey, "Outlook 365 / Microsoft 365", CalendarProvider.Outlook365, Implemented: true));
builder.Services.AddSingleton<ICalendarProviderConnector>(new CalendarProviderInfo("google", "Google Calendar", null, Implemented: false));
builder.Services.AddSingleton<CalendarConnectorRegistry>();
builder.Services.AddScoped<ICalendarEventReader>(sp => sp.GetRequiredService<Outlook365Connector>());
builder.Services.AddHttpClient("Outlook365Token");
builder.Services.AddHttpClient("Outlook365Graph", client =>
{
    client.DefaultRequestHeaders.Add("Prefer", "timezone=Europe/Vilnius");
});

builder.Services.AddValidatorsFromAssemblyContaining<AdminFeatureType>();
builder.Services.AddValidatorsFromAssemblyContaining<TimeTrackingModelValidator>();
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("Default"));

// Persist the Data Protection key ring in the application database so encrypted calendar connector
// tokens survive redeployment and scale-out (local machine key folders do not). See README.
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<ApskaitaDbContext>();

// Authentication: cookie session with Microsoft Entra ID and Google as external OpenIdConnect providers.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
builder.Services.Configure<ConnectorAuthOptions>(builder.Configuration.GetSection(ConnectorAuthOptions.SectionName));
builder.Services.AddScoped<AccountProvisioner>();

var authOptions = builder.Configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

// OIDC options are built lazily on first request (after Build), so the factory is captured and assigned
// after the host is built; ConfigureProvider uses it for the backchannel logging handler.
ILoggerFactory? loggerFactory = null;

var authentication = builder.Services.AddAuthentication(auth =>
{
    auth.DefaultScheme = AuthConstants.CookieScheme;
    auth.DefaultChallengeScheme = AuthConstants.CookieScheme;
})
.AddCookie(AuthConstants.CookieScheme, cookie =>
{
    cookie.LoginPath = "/account/login";
    cookie.LogoutPath = "/account/logout";
    cookie.AccessDeniedPath = "/account/denied";
    cookie.ReturnUrlParameter = "returnUrl";
    cookie.ExpireTimeSpan = TimeSpan.FromHours(8);
    cookie.SlidingExpiration = true;
    cookie.Cookie.HttpOnly = true;
    cookie.Cookie.SameSite = SameSiteMode.Lax;
    cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    cookie.Events = AccountCookieEvents.Create();
});

// Providers are registered only when configured: RemoteAuthenticationOptions.Validate() throws on an
// empty ClientId, and AuthenticationHandlerProvider instantiates every registered scheme's handler on
// each request — so an unconfigured provider would crash the whole app, not just sign-in.
if (!string.IsNullOrWhiteSpace(authOptions.Entra.ClientId))
{
    authentication.AddOpenIdConnect(AuthConstants.EntraScheme, oidc => ConfigureProvider(
        oidc,
        authOptions.Entra.ClientId,
        authOptions.Entra.ClientSecret,
        $"https://login.microsoftonline.com/{authOptions.Entra.TenantId}",
        $"https://login.microsoftonline.com/{authOptions.Entra.TenantId}/v2.0/.well-known/openid-configuration",
        "/signin-entra",
        AuthConstants.EntraScheme));
}

if (!string.IsNullOrWhiteSpace(authOptions.Google.ClientId))
{
    authentication.AddOpenIdConnect(AuthConstants.GoogleScheme, oidc => ConfigureProvider(
        oidc,
        authOptions.Google.ClientId,
        authOptions.Google.ClientSecret,
        "https://accounts.google.com",
        "https://accounts.google.com/.well-known/openid-configuration",
        "/signin-google",
        AuthConstants.GoogleScheme));
}

// Calendar-connector consent round-trip (PAT-001): a DEDICATED multi-tenant app registration (not the
// sign-in Entra app) with Graph delegated scopes, so users from any organization can authenticate and
// consent for their own mailbox. Every challenge sends prompt=select_account so a new connection always
// requires its own authentication instead of silently reusing an existing Microsoft SSO session. The
// callback stores the refresh token against the CURRENT session user keyed by the authenticated account
// (many accounts per provider) and never re-signs the browser in — the existing cookie principal is
// restored in OnTokenValidated and the response is handled there, so the Graph-scoped ticket is never
// written to the session cookie.
if (!string.IsNullOrWhiteSpace(authOptions.Connectors.ClientId))
{
    authentication.AddOpenIdConnect(AuthConstants.ConnectEntraScheme, oidc =>
    {
        oidc.SignInScheme = AuthConstants.CookieScheme;
        oidc.Authority = "https://login.microsoftonline.com/common";
        oidc.MetadataAddress = "https://login.microsoftonline.com/common/v2.0/.well-known/openid-configuration";
        oidc.ClientId = authOptions.Connectors.ClientId;
        oidc.ClientSecret = authOptions.Connectors.ClientSecret;
        oidc.ResponseType = OpenIdConnectResponseType.Code;
        oidc.GetClaimsFromUserInfoEndpoint = false;
        oidc.SaveTokens = true;
        oidc.Scope.Add("openid");
        oidc.Scope.Add("profile");
        oidc.Scope.Add("email");
        oidc.Scope.Add("https://graph.microsoft.com/Calendars.Read");
        oidc.Scope.Add("offline_access");
        oidc.CallbackPath = "/signin-connect-entra";

        oidc.Backchannel = new HttpClient(new ExternalCallLoggingHandler(
            new SocketsHttpHandler(),
            loggerFactory!.CreateLogger("Apskaita.Calendar.Outlook365.Connect")));

        ILogger ConnectLog(HttpContext ctx) => loggerFactory.CreateLogger("Apskaita.Calendar.Connect");

        oidc.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = ctx =>
            {
                // Force the account chooser: each new connection authenticates on its own (REQ-002).
                ctx.ProtocolMessage.Prompt = "select_account";

                var msg = ctx.ProtocolMessage;
                ConnectLog(ctx.HttpContext).LogInformation(
                    "consent redirect: client={ClientId} scope='{Scope}' redirectUri={RedirectUri} prompt={Prompt}",
                    msg.ClientId, msg.Scope, msg.RedirectUri, msg.Prompt);
                return Task.CompletedTask;
            },

            OnTokenValidated = async ctx =>
            {
                var principal = ctx.Principal!;
                var accountEmail = principal.FindFirst("preferred_username")?.Value
                    ?? principal.FindFirst("email")?.Value
                    ?? principal.FindFirst(ClaimTypes.Email)?.Value;
                var displayName = principal.FindFirst("name")?.Value ?? string.Empty;
                string? refreshToken = null;
                if (ctx.Properties?.Items.TryGetValue("refresh_token", out var tokenValue) == true)
                {
                    refreshToken = tokenValue;
                }
                var grantedScopes = ctx.TokenEndpointResponse?.Scope ?? string.Empty;

                var store = ctx.HttpContext.RequestServices.GetRequiredService<CalendarConnectorStore>();
                var protector = ctx.HttpContext.RequestServices.GetRequiredService<ConnectorTokenProtector>();
                var connectLogger = loggerFactory.CreateLogger("Apskaita.Calendar.Connect");

                var accountRoute = ResolveAccountRoute(ctx.HttpContext, "/account/me");

                var returnUrl = ctx.Properties?.RedirectUri;
                if (string.IsNullOrWhiteSpace(returnUrl) || !IsLocalRedirect(returnUrl!))
                {
                    returnUrl = accountRoute;
                }

                if (string.IsNullOrWhiteSpace(accountEmail) || string.IsNullOrWhiteSpace(refreshToken))
                {
                    connectLogger.LogWarning("consent returned no account email or refresh token; connector not stored");
                    ctx.HandleResponse();
                    ctx.Response.Redirect($"{accountRoute}?connectError=1");
                    return;
                }

                var accountKey = accountEmail.Trim().ToLowerInvariant();

                var connector = await store.UpsertOwnAsync(
                    CalendarProvider.Outlook365,
                    accountKey,
                    accountEmail.Trim().ToLowerInvariant(),
                    displayName,
                    protector.Protect(refreshToken),
                    grantedScopes,
                    ctx.HttpContext.RequestAborted);

                if (connector is null)
                {
                    connectLogger.LogWarning("connector could not be stored: no provisioned user for the current session");
                    ctx.HandleResponse();
                    ctx.Response.Redirect($"{accountRoute}?connectError=1");
                    return;
                }

                connectLogger.LogInformation(
                    "calendar connector stored for user subject={Subject} account={Account} scopes='{Scopes}'",
                    store.CurrentEmail, connector.AccountEmail, connector.Scopes);

                // Do not re-issue the session cookie with the Graph-scoped ticket: restore the existing
                // session identity (defensive) and end the round-trip with our own redirect.
                var existing = await ctx.HttpContext.AuthenticateAsync(AuthConstants.CookieScheme);
                if (existing is { Succeeded: true, Principal: not null })
                {
                    ctx.Principal = existing.Principal;
                    ctx.Properties = new AuthenticationProperties { RedirectUri = returnUrl };
                }

                ctx.HandleResponse();
                ctx.Response.Redirect(returnUrl);
            },

            OnRemoteFailure = ctx =>
            {
                ConnectLog(ctx.HttpContext).LogError(ctx.Failure, "calendar consent flow failed");
                ctx.HandleResponse();

                // User consent blocked by the other organization's policy: point at admin consent.
                var failure = ctx.Failure?.Message ?? string.Empty;
                var adminConsent = failure.Contains("AADSTS90093", StringComparison.OrdinalIgnoreCase)
                    || failure.Contains("AADSTS90094", StringComparison.OrdinalIgnoreCase)
                    || failure.Contains("administrator", StringComparison.OrdinalIgnoreCase);

                var accountRoute = ResolveAccountRoute(ctx.HttpContext, "/account/me");
                ctx.Response.Redirect(adminConsent
                    ? $"{accountRoute}?connectError=admin-consent"
                    : $"{accountRoute}?connectError=1");
                return Task.CompletedTask;
            }
        };
    });
}

static string ResolveAccountRoute(HttpContext httpContext, string fallback)
{
    // Derive the account feature's mounted route from the registry instead of hardcoding the prefix.
    var registry = httpContext.RequestServices.GetRequiredService<FeatureRegistry>();
    return registry.TryGetRouteItem("account", out var accountItem) && !string.IsNullOrWhiteSpace(accountItem.Route)
        ? accountItem.Route
        : fallback;
}

static bool IsLocalRedirect(string url) =>
    url.Length > 0 && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) && !url.Contains("://");

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(AuthPolicies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)))
    .AddPolicy(AuthPolicies.Operational, policy => policy.RequireRole(nameof(UserRole.User), nameof(UserRole.Admin)))
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

var app = builder.Build();
var features = app.Services.GetRequiredService<FeatureRegistry>();

loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();
var authLogger = loggerFactory.CreateLogger("Apskaita.Auth");
authLogger.LogInformation(
	"Auth providers configured: entra={Entra} google={Google}; bootstrap admin emails: {AdminEmails}",
	!string.IsNullOrWhiteSpace(authOptions.Entra.ClientId),
	!string.IsNullOrWhiteSpace(authOptions.Google.ClientId),
	string.Join(", ", authOptions.AdminEmails));

app.UseRequestLogging();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.Use(async (context, next) =>
{
	var antiforgery = context.RequestServices.GetRequiredService<Microsoft.AspNetCore.Antiforgery.IAntiforgery>();
	var tokens = antiforgery.GetAndStoreTokens(context);
	context.Items["RequestVerificationToken"] = tokens.RequestToken ?? string.Empty;

	await next();
});

// Authenticated users without an assigned role land on the pending-access page instead of feature routes.
app.Use(async (context, next) =>
{
	var user = context.User;
	if (context.Request.Path == "/"
		&& user.Identity?.IsAuthenticated == true
		&& !user.IsInRole(nameof(UserRole.User))
		&& !user.IsInRole(nameof(UserRole.Admin)))
	{
		context.Response.Redirect("/account/pending");
		return;
	}

	await next();
});

app.UseHtmxErrorHandling();
app.UseAntiforgery();

features.MapEndpoints(app);
app.MapFeaturePages(features);

app.Run();

/// <summary>
/// Applies the shared OpenIdConnect configuration to a provider registration and tags the resulting
/// cookie sign-in with the provider key (consumed by <see cref="AccountCookieEvents"/>). Every external
/// interaction is logged: browser redirects to the provider's authorization endpoint, backchannel
/// metadata/token calls, provider failures, and the validated identity.
/// </summary>
void ConfigureProvider(
	OpenIdConnectOptions oidc,
	string clientId,
	string clientSecret,
	string authority,
	string metadataAddress,
	string callbackPath,
	string providerKey)
{
	oidc.SignInScheme = AuthConstants.CookieScheme;
	oidc.Authority = authority;
	oidc.MetadataAddress = metadataAddress;
	oidc.ClientId = clientId;
	oidc.ClientSecret = clientSecret;
	oidc.ResponseType = OpenIdConnectResponseType.Code;
	oidc.GetClaimsFromUserInfoEndpoint = false;
	oidc.SaveTokens = false;
	oidc.Scope.Add("openid");
	oidc.Scope.Add("profile");
	oidc.Scope.Add("email");
	oidc.CallbackPath = callbackPath;

	// Server-side calls to the provider (metadata discovery, JWKS, token exchange) go through this handler.
	oidc.Backchannel = new HttpClient(new ExternalCallLoggingHandler(
		new SocketsHttpHandler(),
		loggerFactory!.CreateLogger($"Apskaita.Auth.{providerKey}.External")));

	ILogger Log(HttpContext ctx) => loggerFactory.CreateLogger($"Apskaita.Auth.{providerKey}");

	oidc.Events = new OpenIdConnectEvents
	{
		OnRedirectToIdentityProvider = ctx =>
		{
			var msg = ctx.ProtocolMessage;
			Log(ctx.HttpContext).LogInformation(
				"redirecting browser to provider authorize endpoint: client={ClientId} scope='{Scope}' redirectUri={RedirectUri} responseType={ResponseType}",
				msg.ClientId, msg.Scope, msg.RedirectUri, msg.ResponseType);
			return Task.CompletedTask;
		},
		OnMessageReceived = ctx =>
		{
			var msg = ctx.ProtocolMessage;
			Log(ctx.HttpContext).LogInformation("callback received at {Path}: code={Code} state={State} error={Error}",
				ctx.HttpContext.Request.Path,
				string.IsNullOrEmpty(msg?.Code) ? "-" : "yes",
				string.IsNullOrEmpty(msg?.State) ? "-" : "yes",
				msg?.Error ?? "-");
			return Task.CompletedTask;
		},
		OnRemoteFailure = ctx =>
		{
			Log(ctx.HttpContext).LogError(ctx.Failure, "provider reported a failure during the sign-in flow");
			return Task.CompletedTask;
		},
		OnTokenValidated = ctx =>
		{
			var principal = ctx.Principal!;
			var email = principal.FindFirst("email")?.Value
				?? principal.FindFirst("preferred_username")?.Value
				?? principal.FindFirst(ClaimTypes.Email)?.Value;
			var subject = principal.FindFirst("oid")?.Value
				?? principal.FindFirst("sub")?.Value
				?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
			Log(ctx.HttpContext).LogInformation("token validated for subject={Subject} email={Email}", subject, email);
			ctx.Properties!.Items[AuthConstants.ProviderItemKey] = providerKey;
			return Task.CompletedTask;
		}
	};
}

/// <summary>
/// Exposes the top-level-statement entry point type so <c>WebApplicationFactory&lt;Program&gt;</c>
/// can boot the app in integration tests.
/// </summary>
public partial class Program;
