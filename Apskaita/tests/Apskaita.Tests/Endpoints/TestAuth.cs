using System.Security.Claims;
using System.Text.Encodings.Web;
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
