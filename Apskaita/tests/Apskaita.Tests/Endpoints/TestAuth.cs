using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using Apskaita.Entities;
using Apskaita.Feature.Account;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Apskaita.Tests.Endpoints;

/// <summary>
/// Holds the <see cref="ClaimsPrincipal"/> the stub authentication handler returns for the current test.
/// Registered as a singleton by <see cref="ApskaitaApiFactory"/>.
/// </summary>
public sealed class TestPrincipalProvider
{
    public ClaimsPrincipal? Principal { get; set; }
}

/// <summary>
/// Authentication handler that authenticates whatever principal <see cref="TestPrincipalProvider"/> holds,
/// replacing the real cookie/OIDC stack in integration tests (CON-003). Challenges still fall through to
/// the cookie scheme so unauthenticated requests exercise the real login redirect behavior.
/// </summary>
public sealed class TestStubHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Apskaita.Test";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var provider = Context.RequestServices.GetRequiredService<TestPrincipalProvider>();
        return provider.Principal is null
            ? Task.FromResult(AuthenticateResult.NoResult())
            : Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(provider.Principal, SchemeName)));
    }
}

/// <summary>
/// Builds the principal shape the application produces after provisioning (name = email, display-name and
/// role claims), so tests exercise the same authorization policies as production.
/// </summary>
public static class TestPrincipal
{
    public static ClaimsPrincipal Build(string email, UserRole? role, Guid? userId = null)
    {
        var identity = new ClaimsIdentity(
            authenticationType: TestStubHandler.SchemeName,
            nameType: ClaimTypes.Name,
            roleType: ClaimTypes.Role);

        identity.AddClaim(new Claim(ClaimTypes.Name, email));
        identity.AddClaim(new Claim(AuthConstants.DisplayNameClaim, email));

        if (userId is { } id)
        {
            identity.AddClaim(new Claim(AuthConstants.UserIdClaim, id.ToString()));
        }

        if (role.HasValue)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role.Value.ToString()));
        }

        return new ClaimsPrincipal(identity);
    }
}

/// <summary>
/// Antiforgery helper for integration tests: the layout emits the token as a
/// <c>&lt;meta name="request-verification-token"&gt;</c> tag and the client-side htmx hook sends it as
/// the <c>RequestVerificationToken</c> header. Tests POSTing forms emulate the browser by scraping the
/// token from a rendered page.
/// </summary>
public static class TestAntiforgery
{
    private static readonly Regex TokenRegex = new("request-verification-token\"\\s+content=\"([^\"]+)\"", RegexOptions.Compiled);

    /// <summary>Scrapes the antiforgery token from the given page (full-page GET) of the running app.</summary>
    public static async Task<string> GetTokenAsync(HttpClient http, string pageUrl, CancellationToken ct = default)
    {
        var html = await http.GetStringAsync(pageUrl, ct);
        var match = TokenRegex.Match(html);
        Assert.True(match.Success, $"Antiforgery token should be present on {pageUrl}.");
        return match.Groups[1].Value;
    }
}

/// <summary>
/// HTMX request helper: fragment endpoints answer HTMX requests only, so tests fetch them the way the
/// browser does (<c>HX-Request</c> plus the optional <c>HX-Target</c> the element would send).
/// </summary>
public static class TestHtmx
{
    /// <summary>GETs a URL as an HTMX request and returns the response body (asserts 2xx).</summary>
    public static async Task<string> GetHtmlAsync(HttpClient http, string url, string? target = null, string? currentUrl = null, CancellationToken ct = default)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.TryAddWithoutValidation("HX-Request", "true");
        if (target is not null)
        {
            request.Headers.TryAddWithoutValidation("HX-Target", target);
        }

        // htmx always reports the browser URL; the nav OOB strategy uses it to skip navigation for
        // same-feature fragment requests (as it does in the live app).
        request.Headers.TryAddWithoutValidation("HX-Current-URL", currentUrl ?? "http://localhost" + url);

        var response = await http.SendAsync(request, ct);
        Assert.True(response.IsSuccessStatusCode, $"Expected 200 for {url}, got {(int)response.StatusCode}.");
        return await response.Content.ReadAsStringAsync(ct);
    }
}
