using System.Security.Claims;
using Apskaita.Web.Features.Account.Models;
using Apskaita.Web.Features.Account.Slices;
using Microsoft.AspNetCore.Authentication;

namespace Apskaita.Web.Features.Account.Endpoints;

/// <summary>
/// Authentication feature: login page, provider challenges, cookie sign-out, and the pending-access landing
/// page for roleless users. Login and challenge endpoints are anonymous; logout and pending require an
/// authenticated session (CON-004) but no role.
/// </summary>
public sealed class AccountEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    public override IReadOnlyList<NavigationItem> NavigationItems => [];

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
    }

    private static bool IsLocalUrl(string? url) =>
        !string.IsNullOrEmpty(url) &&
        url[0] == '/' &&
        (url.Length == 1 || (url[1] != '/' && url[1] != '\\')) &&
        !url.Contains("://");
}
