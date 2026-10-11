using System.Security.Claims;
using Apskaita.Web.Features.Account.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Primitives;

namespace Apskaita.Web.Features.Account.Authentication;

/// <summary>
/// Cookie-scheme events implementing the shared sign-in hook (GUD-002): provider-agnostic provisioning via
/// <see cref="AccountProvisioner"/> and HTMX-aware login redirects (REQ-010).
/// </summary>
public static class AccountCookieEvents
{
    public static CookieAuthenticationEvents Create() => new()
    {
        OnSigningIn = async context =>
        {
            var external = context.Principal!;

            var email = GetClaimValue(external, ClaimTypes.Email, "email", "preferred_username");
            var displayName = GetClaimValue(external, ClaimTypes.Name, "name") ?? string.Empty;
            var subjectId = GetClaimValue(external, "oid", "sub", ClaimTypes.NameIdentifier) ?? string.Empty;
            var provider = context.Properties.Items.TryGetValue(AuthConstants.ProviderItemKey, out var p) ? p : null;

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(provider))
            {
                // Fail closed: no provider-asserted email means no identity (SEC-001).
                context.Principal = null;
                return;
            }

            var provisioner = context.HttpContext.RequestServices.GetRequiredService<AccountProvisioner>();
            var result = await provisioner.ProvisionAsync(
                email!, displayName, provider!, subjectId, context.HttpContext.RequestAborted);

            if (!result.Allowed)
            {
                context.Principal = null;
                return;
            }

            var identity = new ClaimsIdentity(
                authenticationType: AuthConstants.CookieScheme,
                nameType: ClaimTypes.Name,
                roleType: ClaimTypes.Role);

            identity.AddClaim(new Claim(ClaimTypes.Name, email!));
            identity.AddClaim(new Claim(AuthConstants.DisplayNameClaim, string.IsNullOrWhiteSpace(displayName) ? email! : displayName));
            identity.AddClaim(new Claim(AuthConstants.ProviderClaim, provider!));

            if (result.Role.HasValue)
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, result.Role.Value.ToString()));
            }

            context.Principal = new ClaimsPrincipal(identity);
        },

        OnRedirectToLogin = context =>
        {
            // HTMX fragment calls must not swap the login page into a detail pane (REQ-010).
            if (IsHtmxRequest(context.Request.Headers["HX-Request"]))
            {
                context.Response.StatusCode = StatusCodes.Status204NoContent;
                context.Response.Headers["HX-Redirect"] = context.RedirectUri;
                return Task.CompletedTask;
            }

            context.Response.Redirect(context.RedirectUri);
            return Task.CompletedTask;
        }
    };

    private static string? GetClaimValue(ClaimsPrincipal principal, params string[] claimTypes)
    {
        foreach (var claimType in claimTypes)
        {
            var value = principal.FindFirst(claimType)?.Value;
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }

    private static bool IsHtmxRequest(StringValues header) =>
        StringValues.IsNullOrEmpty(header) == false &&
        header.Any(v => string.Equals(v, "true", StringComparison.OrdinalIgnoreCase));
}
