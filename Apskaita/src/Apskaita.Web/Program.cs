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
using FluentValidation;
using Apskaita.Entities;
using Apskaita.Web.Features.TimeTracking.Endpoints;
using Apskaita.Web.Features.TimeTracking.Validators;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.Routing;
using Apskaita.Web.Logging;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
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

builder.Services.AddValidatorsFromAssemblyContaining<AdminFeatureType>();
builder.Services.AddValidatorsFromAssemblyContaining<TimeTrackingModelValidator>();
builder.Services.AddDataAccess(builder.Configuration.GetConnectionString("Default"));

// Authentication: cookie session with Microsoft Entra ID and Google as external OpenIdConnect providers.
builder.Services.Configure<AuthOptions>(builder.Configuration.GetSection(AuthOptions.SectionName));
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

	ILogger Log(HttpContext ctx) => loggerFactory!.CreateLogger($"Apskaita.Auth.{providerKey}");

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
