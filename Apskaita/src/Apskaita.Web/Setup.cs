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
using Apskaita.Web.Composition;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Microsoft.IdentityModel.JsonWebTokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.DependencyInjection;
using AdminFeatureType = Apskaita.Web.Features.Admin.ClientsRegistry.Models.ClientListQuery;

namespace Apskaita.Web;

/// <summary>
/// Decomposed startup configuration for the Apskaita web application. Each method groups one startup
/// concern that previously lived inline in <c>Program.cs</c>, so <c>Program.cs</c> stays a thin, readable
/// orchestrator (and <c>WebApplicationFactory&lt;Program&gt;</c> keeps working).
/// </summary>
public static class Setup
{
    // -------------------------------------------------------------------------------------------
    // Service registration
    // -------------------------------------------------------------------------------------------

    /// <summary>RazorSlices + HTMX core services, the Bootstrap 5 adapter, the page renderer, and antiforgery/context plumbing.</summary>
    public static void RazorSlices(IServiceCollection services)
    {
        services.AddRazorSlicesHtmx();
        services.AddAntiforgery();
        services.AddHttpContextAccessor();
        services.AddScoped<IFeatureUri, HttpFeatureUri>();
        services.AddRazorSlicesHtmxBootstrap5();
        services.AddSingleton<IFeaturePageRenderer, AppFeaturePageRenderer>();
    }

    /// <summary>Registers every feature module under its route prefix.</summary>
    public static void Features(IServiceCollection services)
    {
        services.AddFeature<TimeTrackingEndpoints>("/time-tracking");
        services.AddFeature<ClientsEndpoints>("/admin/clients");
        services.AddFeature<IssuersEndpoints>("/admin/issuers");
        services.AddFeature<UnitsEndpoints>("/admin/units");
        services.AddFeature<ProjectsEndpoints>("/admin/projects");
        services.AddFeature<InvoiceTemplatesEndpoints>("/admin/invoice-templates");
        services.AddFeature<SystemSettingsEndpoints>("/admin/system-settings");
        services.AddFeature<UsersEndpoints>("/admin/users");
        services.AddFeature<AccountEndpoints>("/account");
    }

    /// <summary>
    /// Calendar connectors (feature-calendar-connectors-1): provider registry + Outlook 365 Graph connector.
    /// Feature modules are singletons, so the registry holds singleton provider metadata only; the scoped
    /// connector (and the store it uses) is resolved per request inside endpoints.
    /// </summary>
    public static void CalendarConnectors(IServiceCollection services)
    {
        services.AddSingleton<ConnectorTokenProtector>();
        services.AddScoped<CalendarConnectorStore>();
        services.AddScoped<Outlook365Connector>(sp => new Outlook365Connector(
            sp.GetRequiredService<CalendarConnectorStore>(),
            sp.GetRequiredService<ConnectorTokenProtector>(),
            sp.GetRequiredService<IHttpClientFactory>(),
            sp.GetRequiredService<IOptions<ConnectorAuthOptions>>(),
            sp.GetRequiredService<ILoggerFactory>().CreateLogger("Apskaita.Calendar.Outlook365"),
            sp.GetRequiredService<IHttpContextAccessor>()));
        services.AddSingleton<ICalendarProviderConnector>(new CalendarProviderInfo(Outlook365Connector.ProviderKey, "Outlook 365 / Microsoft 365", CalendarProvider.Outlook365, Implemented: true));
        services.AddSingleton<ICalendarProviderConnector>(new CalendarProviderInfo("google", "Google Calendar", null, Implemented: false));
        services.AddSingleton<CalendarConnectorRegistry>();
        services.AddScoped<ICalendarEventReader>(sp => sp.GetRequiredService<Outlook365Connector>());
        services.AddHttpClient("Outlook365Token");
        // No Prefer: timezone header — the connector sends UTC (Z) boundaries and converts to local time
        // itself; a Prefer timezone contradicts Z datetimes and makes Graph return timezone-less values.
        services.AddHttpClient("Outlook365Graph");
    }

    /// <summary>FluentValidation validators from the admin and time-tracking assemblies.</summary>
    public static void Validation(IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<AdminFeatureType>();
        services.AddValidatorsFromAssemblyContaining<TimeTrackingModelValidator>();
    }

    /// <summary>
    /// EF Core data access plus the database-backed Data Protection key ring so encrypted calendar
    /// connector tokens survive redeployment and scale-out (local machine key folders do not). See README.
    /// </summary>
    public static void EfCore(IServiceCollection services, IConfiguration config)
    {
        services.AddDataAccess(config.GetConnectionString("Default"));
        services.AddDataProtection()
            .PersistKeysToDbContext<ApskaitaDbContext>();
        // Cross-feature read seam: one open-generic no-tracking IQuery<> over the shared context.
        services.AddScoped(typeof(IQuery<>), typeof(Query<>));
        // Feature write stores: composition adapters over the shared context, registered against each
        // feature's internal store interface (visible via InternalsVisibleTo).
        services.AddScoped<IAccountStore, AccountStore>();
    }

    /// <summary>
    /// Authentication (cookie session with Microsoft Entra ID and Google as external OpenIdConnect providers,
    /// plus the dedicated multi-tenant calendar-connector consent scheme) and the authorization policies.
    /// Providers are registered only when configured: RemoteAuthenticationOptions.Validate() throws on an
    /// empty ClientId, and AuthenticationHandlerProvider instantiates every registered scheme's handler on
    /// each request — so an unconfigured provider would crash the whole app, not just sign-in.
    /// <para>
    /// The options that depend on DI (the backchannel handler and the event handlers, which need an
    /// <see cref="ILoggerFactory"/>) are configured through <c>AddOptions&lt;OpenIdConnectOptions&gt;(scheme)
    /// .Configure&lt;ILoggerFactory&gt;</c> rather than captured from the builder. The options instance is
    /// application-lifetime, so the backchannel uses a pooled <see cref="SocketsHttpHandler"/> (recycled
    /// connections respect DNS changes and avoid socket exhaustion) wrapped by the external-call logging
    /// handler.
    /// </para>
    /// </summary>
    public static void EntraId(IServiceCollection services, IConfiguration config)
    {
        services.Configure<AuthOptions>(config.GetSection(AuthOptions.SectionName));
        services.Configure<ConnectorAuthOptions>(config.GetSection(ConnectorAuthOptions.SectionName));
        services.AddScoped<AccountProvisioner>();

        var authOptions = config.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        var authentication = services.AddAuthentication(auth =>
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

        if (!string.IsNullOrWhiteSpace(authOptions.Entra.ClientId))
        {
            authentication.AddOpenIdConnect(AuthConstants.EntraScheme, oidc => ConfigureProvider(
                oidc,
                authOptions.Entra.ClientId,
                authOptions.Entra.ClientSecret,
                $"https://login.microsoftonline.com/{authOptions.Entra.TenantId}",
                $"https://login.microsoftonline.com/{authOptions.Entra.TenantId}/v2.0/.well-known/openid-configuration",
                "/signin-entra"));

            services.AddOptions<OpenIdConnectOptions>(AuthConstants.EntraScheme)
                .Configure<ILoggerFactory>((oidc, loggerFactory) => ConfigureProviderEvents(oidc, AuthConstants.EntraScheme, loggerFactory));
        }

        if (!string.IsNullOrWhiteSpace(authOptions.Google.ClientId))
        {
            authentication.AddOpenIdConnect(AuthConstants.GoogleScheme, oidc => ConfigureProvider(
                oidc,
                authOptions.Google.ClientId,
                authOptions.Google.ClientSecret,
                "https://accounts.google.com",
                "https://accounts.google.com/.well-known/openid-configuration",
                "/signin-google"));

            services.AddOptions<OpenIdConnectOptions>(AuthConstants.GoogleScheme)
                .Configure<ILoggerFactory>((oidc, loggerFactory) => ConfigureProviderEvents(oidc, AuthConstants.GoogleScheme, loggerFactory));
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
            authentication.AddOpenIdConnect(AuthConstants.ConnectEntraScheme, oidc => ConfigureConnector(oidc, authOptions));

            services.AddOptions<OpenIdConnectOptions>(AuthConstants.ConnectEntraScheme)
                .Configure<ILoggerFactory>(ConfigureConnectorEvents);
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)))
            .AddPolicy(AuthPolicies.Operational, policy => policy.RequireRole(nameof(UserRole.User), nameof(UserRole.Admin)))
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
    }

    /// <summary>
    /// Enables the framework's extended HTTP client logging (Microsoft.Extensions.Http.Diagnostics) for every
    /// IHttpClientFactory client, configured from the "HttpClientLogging" config section. Requires redaction
    /// services. Body logging is dev-only (appsettings.Development.json) and surfaces Graph/token bodies as
    /// structured-log properties, rendered by the Serilog {Properties:j} console template.
    /// </summary>
    public static void HttpClientLogging(IServiceCollection services, IConfiguration config)
    {
        services.AddRedaction();
        services.AddExtendedHttpClientLogging(config.GetSection("HttpClientLogging"));
    }

    // -------------------------------------------------------------------------------------------
    // Host / pipeline (run after builder.Build())
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Logs the configured authentication providers and installs the request-logging middleware. Must run
    /// immediately after <c>builder.Build()</c> and before the first request.
    /// </summary>
    public static void Logging(WebApplication app)
    {
        var loggerFactory = app.Services.GetRequiredService<ILoggerFactory>();

        var config = app.Services.GetRequiredService<IConfiguration>();
        var authOptions = config.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();

        loggerFactory.CreateLogger("Apskaita.Auth").LogInformation(
            "Auth providers configured: entra={Entra} google={Google}; bootstrap admin emails: {AdminEmails}",
            !string.IsNullOrWhiteSpace(authOptions.Entra.ClientId),
            !string.IsNullOrWhiteSpace(authOptions.Google.ClientId),
            string.Join(", ", authOptions.AdminEmails));

        app.UseRequestLogging();
    }

    /// <summary>
    /// The request pipeline: static files, authentication/authorization, the antiforgery token store, the
    /// roleless-user pending redirect, and HTMX error handling.
    /// </summary>
    public static void Pipeline(WebApplication app)
    {
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
    }

    /// <summary>Maps every feature module's endpoints and the feature page routes.</summary>
    public static void Endpoints(WebApplication app)
    {
        var features = app.Services.GetRequiredService<FeatureRegistry>();
        features.MapEndpoints(app);
        app.MapFeaturePages(features);
    }

    // -------------------------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Applies the static OpenIdConnect configuration to a sign-in provider registration (authority,
    /// credentials, scopes, callback path). The DI-dependent backchannel and event handlers are applied
    /// separately by <see cref="ConfigureProviderEvents"/>.
    /// </summary>
    private static void ConfigureProvider(
        OpenIdConnectOptions oidc,
        string clientId,
        string clientSecret,
        string authority,
        string metadataAddress,
        string callbackPath)
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
    }

    /// <summary>
    /// Applies the DI-resolved backchannel and event handlers to a sign-in provider. Every external
    /// interaction is logged: browser redirects to the provider's authorization endpoint, backchannel
    /// metadata/token calls, provider failures, and the validated identity. The resulting cookie sign-in is
    /// tagged with the provider key (consumed by <see cref="AccountCookieEvents"/>).
    /// </summary>
    private static void ConfigureProviderEvents(
        OpenIdConnectOptions oidc,
        string providerKey,
        ILoggerFactory loggerFactory)
    {
        // Server-side calls to the provider (metadata discovery, JWKS, token exchange) go through this
        // handler. The options instance is application-lifetime, so a pooled handler recycles connections
        // (DNS changes) without socket exhaustion; the delegating handler adds the external-call logging.
        oidc.BackchannelHttpHandler = new ExternalCallLoggingHandler(
            new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) },
            loggerFactory.CreateLogger($"Apskaita.Auth.{providerKey}.External"));

        ILogger Log() => loggerFactory.CreateLogger($"Apskaita.Auth.{providerKey}");

        oidc.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = ctx =>
            {
                var msg = ctx.ProtocolMessage;
                Log().LogInformation(
                    "redirecting browser to provider authorize endpoint: client={ClientId} scope='{Scope}' redirectUri={RedirectUri} responseType={ResponseType}",
                    msg.ClientId, msg.Scope, msg.RedirectUri, msg.ResponseType);
                return Task.CompletedTask;
            },
            OnMessageReceived = ctx =>
            {
                var msg = ctx.ProtocolMessage;
                Log().LogInformation("callback received at {Path}: code={Code} state={State} error={Error}",
                    ctx.HttpContext.Request.Path,
                    string.IsNullOrEmpty(msg.Code) ? "-" : "yes",
                    string.IsNullOrEmpty(msg.State) ? "-" : "yes",
                    msg.Error ?? "-");
                return Task.CompletedTask;
            },
            OnRemoteFailure = ctx =>
            {
                Log().LogError(ctx.Failure, "provider reported a failure during the sign-in flow");
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
                Log().LogInformation("token validated for subject={Subject} email={Email}", subject, email);
                ctx.Properties!.Items[AuthConstants.ProviderItemKey] = providerKey;
                return Task.CompletedTask;
            }
        };
    }

    /// <summary>
    /// Configures the static options for the dedicated multi-tenant calendar-connector consent scheme
    /// (<c>connect-entra</c>). Uses the tenant-independent <c>/common</c> metadata, so the vanilla issuer
    /// exact-match would fail with IDX10205; a custom issuer validator (see
    /// <see cref="ValidateMultiTenantConnectorIssuer"/>) replaces it. The DI-dependent backchannel and event
    /// handlers are applied by <see cref="ConfigureConnectorEvents"/>.
    /// </summary>
    private static void ConfigureConnector(OpenIdConnectOptions oidc, AuthOptions authOptions)
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

        // form_post is a cross-site POST, so the SameSite=Lax session cookie is not sent; a GET callback carries it.
        oidc.ResponseMode = OpenIdConnectResponseMode.Query;

        // Multi-tenant connector app (AzureADMultipleOrgs) + tenant-independent /common metadata: the
        // published issuer is the template https://login.microsoftonline.com/{tenantid}/v2.0 while each
        // token's iss claim is the signer's home tenant (.../{tid}/v2.0). The vanilla OpenIdConnect handler
        // exact-matches the issuer and does NOT substitute {tenantid} (only Microsoft Identity Web does),
        // so validation fails with IDX10205. Use the documented multi-tenant issuer validator instead.
        oidc.TokenValidationParameters.IssuerValidator = ValidateMultiTenantConnectorIssuer;
    }

    /// <summary>
    /// Applies the DI-resolved backchannel and event handlers to the calendar-connector consent scheme.
    /// </summary>
    private static void ConfigureConnectorEvents(OpenIdConnectOptions oidc, ILoggerFactory loggerFactory)
    {
        oidc.BackchannelHttpHandler = new ExternalCallLoggingHandler(
            new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(5) },
            loggerFactory.CreateLogger("Apskaita.Calendar.Outlook365.Connect"));

        ILogger ConnectLog() => loggerFactory.CreateLogger("Apskaita.Calendar.Connect");

        oidc.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = ctx =>
            {
                // Force the account chooser: each new connection authenticates on its own (REQ-002).
                ctx.ProtocolMessage.Prompt = "select_account";

                var msg = ctx.ProtocolMessage;
                ConnectLog().LogInformation(
                    "consent redirect: client={ClientId} scope='{Scope}' redirectUri={RedirectUri} prompt={Prompt}",
                    msg.ClientId, msg.Scope, msg.RedirectUri, msg.Prompt);
                return Task.CompletedTask;
            },

            OnTokenValidated = async ctx =>
            {
                var connectLogger = ConnectLog();
                var accountRoute = ResolveAccountRoute(ctx.HttpContext, "/account/me");

                var returnUrl = ctx.Properties?.RedirectUri;
                if (string.IsNullOrWhiteSpace(returnUrl) || !IsLocalRedirect(returnUrl))
                {
                    returnUrl = accountRoute;
                }

                // The OIDC callback handler runs BEFORE the cookie scheme's default authentication in the
                // middleware pipeline, so HttpContext.User is still anonymous here and CalendarConnectorStore
                // cannot resolve the owner. Authenticate the session cookie explicitly and make its principal
                // current so the upsert keys off the session's userId claim (SEC-002: owner always from session).
                var existing = await ctx.HttpContext.AuthenticateAsync(AuthConstants.CookieScheme);
                if (existing is not { Succeeded: true, Principal: not null })
                {
                    connectLogger.LogWarning("connector not stored: no authenticated session for the connect round-trip");
                    ctx.HandleResponse();
                    ctx.Response.Redirect($"{accountRoute}?connectError=1");
                    return;
                }
                ctx.HttpContext.User = existing.Principal;

                var principal = ctx.Principal!;
                var accountEmail = principal.FindFirst("preferred_username")?.Value
                    ?? principal.FindFirst("email")?.Value
                    ?? principal.FindFirst(ClaimTypes.Email)?.Value;
                var displayName = principal.FindFirst("name")?.Value ?? string.Empty;

                // The refresh token lives on the raw token response, which is available synchronously here.
                // ctx.Properties.Items["refresh_token"] is unreliable: the handler stores saved tokens into
                // the properties AFTER OnTokenValidated runs, so they are not present during the event.
                var refreshToken = ctx.TokenEndpointResponse?.RefreshToken;
                var grantedScopes = ctx.TokenEndpointResponse?.Scope ?? string.Empty;

                var store = ctx.HttpContext.RequestServices.GetRequiredService<CalendarConnectorStore>();
                var protector = ctx.HttpContext.RequestServices.GetRequiredService<ConnectorTokenProtector>();

                if (string.IsNullOrWhiteSpace(accountEmail) || string.IsNullOrWhiteSpace(refreshToken))
                {
                    connectLogger.LogWarning(
                        "connector not stored: accountEmail={AccountEmail} refreshTokenPresent={RefreshTokenPresent} grantedScopes='{GrantedScopes}' idTokenClaims=[{Claims}]",
                        accountEmail ?? "(none)",
                        !string.IsNullOrWhiteSpace(refreshToken),
                        grantedScopes,
                        string.Join(", ", principal.Claims.Select(c => c.Type)));
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
                    connectLogger.LogWarning("connector could not be stored: session has no userId claim — sign out and sign back in to refresh the session cookie");
                    ctx.HandleResponse();
                    ctx.Response.Redirect($"{accountRoute}?connectError=1");
                    return;
                }

                connectLogger.LogInformation(
                    "calendar connector stored for user subject={Subject} account={Account} scopes='{Scopes}'",
                    store.CurrentEmail, connector.AccountEmail, connector.Scopes);

                // Do not re-issue the session cookie with the Graph-scoped ticket: restore the existing
                // session identity (defensive) and end the round-trip with our own redirect.
                ctx.Principal = existing.Principal;
                ctx.Properties = new AuthenticationProperties { RedirectUri = returnUrl };

                ctx.HandleResponse();
                ctx.Response.Redirect(returnUrl);
            },

            OnRemoteFailure = ctx =>
            {
                ConnectLog().LogError(ctx.Failure, "calendar consent flow failed");
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
    }

    /// <summary>
    /// Derives the account feature's mounted route from the registry instead of hardcoding the prefix.
    /// </summary>
    private static string ResolveAccountRoute(HttpContext httpContext, string fallback)
    {
        var registry = httpContext.RequestServices.GetRequiredService<FeatureRegistry>();
        return registry.TryGetRouteItem("account", out var accountItem) && !string.IsNullOrWhiteSpace(accountItem.Route)
            ? accountItem.Route
            : fallback;
    }

    private static bool IsLocalRedirect(string url) =>
        url.Length > 0 && url[0] == '/' && (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) && !url.Contains("://");

    /// <summary>
    /// Validates the connector token issuer for the multi-tenant /common authority. Replaces {tenantid} in the
    /// metadata issuer template with the token's tid claim and requires an exact match, and requires tid to be
    /// a GUID so the issuer is tied back to the signing key's scope (Entra multi-tenant issuer validation).
    /// </summary>
    private static string ValidateMultiTenantConnectorIssuer(
        string issuer,
        SecurityToken securityToken,
        TokenValidationParameters validationParameters)
    {
        string? tid = securityToken switch
        {
            JwtSecurityToken jwt => jwt.Claims.FirstOrDefault(c => c.Type == "tid")?.Value,
            JsonWebToken json => json.Claims.FirstOrDefault(c => c.Type == "tid")?.Value,
            _ => null,
        };

        var template = validationParameters.ValidIssuer
            ?? validationParameters.ValidIssuers?.FirstOrDefault()
            ?? "https://login.microsoftonline.com/{tenantid}/v2.0";

        var expected = template.Replace("{tenantid}", tid ?? string.Empty, StringComparison.OrdinalIgnoreCase);

        if (!Guid.TryParse(tid, out _) ||
            !string.Equals(issuer, expected, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenInvalidIssuerException(
                $"Connector token issuer '{issuer}' failed multi-tenant validation; expected '{expected}' " +
                $"derived from the token 'tid' claim '{tid ?? string.Empty}'.")
            {
                InvalidIssuer = issuer,
            };
        }

        return issuer;
    }
}
