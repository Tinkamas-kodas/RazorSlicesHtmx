using Apskaita.Shared.Calendar;

namespace Apskaita.Feature.Account;

/// <summary>
/// Strongly typed binding for the <c>Auth</c> configuration section: external provider credentials and the
/// bootstrap-admin email list. Secrets must come from user-secrets (dev) or environment/secret store (prod).
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public EntraAuthOptions Entra { get; set; } = new();

    public GoogleAuthOptions Google { get; set; } = new();

    /// <summary>
    /// Dedicated app registration for calendar-connector consent. Kept separate from <see cref="Entra"/>
    /// (sign-in) so connectors are multi-tenant and a new connection always requires its own authentication.
    /// </summary>
    public ConnectorAuthOptions Connectors { get; set; } = new();

    /// <summary>
    /// Emails that receive the <c>admin</c> role automatically on sign-in when the user has no role yet.
    /// Solves the chicken-and-egg problem of who assigns the first role. Matched case-insensitively.
    /// </summary>
    public List<string> AdminEmails { get; set; } = [];
}

/// <summary>Microsoft Entra ID (OpenIdConnect) settings.</summary>
public sealed class EntraAuthOptions
{
    /// <summary>Tenant route: a tenant GUID, <c>common</c>, <c>organizations</c>, or <c>consumers</c>.</summary>
    public string TenantId { get; set; } = "common";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}

/// <summary>Google (OpenIdConnect) settings.</summary>
public sealed class GoogleAuthOptions
{
    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
