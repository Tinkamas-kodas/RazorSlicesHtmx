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

    /// <summary>
    /// OpenIdConnect scheme for the calendar-connector consent round-trip (Outlook 365). It uses the
    /// dedicated multi-tenant connector app and requests Graph scopes; it never re-signs the browser in:
    /// the callback stores the refresh token against the existing session user and redirects back to the
    /// account page.
    /// </summary>
    public const string ConnectEntraScheme = "connect-entra";

    /// <summary>Authentication properties item key carrying the external provider that produced the sign-in.</summary>
    public const string ProviderItemKey = "idp";

    /// <summary>Custom claim carrying the provider display name.</summary>
    public const string DisplayNameClaim = "displayName";

    /// <summary>Custom claim carrying the external provider (<c>entra</c>/<c>google</c>).</summary>
    public const string ProviderClaim = "identityProvider";

    /// <summary>
    /// Custom claim carrying the <c>users.id</c> of the provisioned user. Issued at sign-in so per-user data
    /// (calendar connectors, audit fields) resolves the owner from the session without a Users-table lookup.
    /// </summary>
    public const string UserIdClaim = "userId";
}
