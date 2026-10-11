namespace Apskaita.Web.Features.Account;

/// <summary>
/// Scheme, policy, and claim-type constants for the authentication stack.
/// </summary>
public static class AuthConstants
{
    /// <summary>The session cookie scheme (the only scheme that persists a browser session).</summary>
    public const string CookieScheme = "Apskaita.Cookie";

    /// <summary>OpenIdConnect scheme name for Microsoft Entra ID.</summary>
    public const string EntraScheme = "entra";

    /// <summary>OpenIdConnect scheme name for Google.</summary>
    public const string GoogleScheme = "google";

    /// <summary>Authentication properties item key carrying the external provider that produced the sign-in.</summary>
    public const string ProviderItemKey = "idp";

    /// <summary>Custom claim carrying the provider display name.</summary>
    public const string DisplayNameClaim = "displayName";

    /// <summary>Custom claim carrying the external provider (<c>entra</c>/<c>google</c>).</summary>
    public const string ProviderClaim = "identityProvider";
}
