namespace Apskaita.Web.Shared.Calendar;

/// <summary>
/// Calendar-connector consent app (Microsoft Graph delegated scopes). Multi-tenant: authority is always
/// <c>common</c> so users from any organization can authenticate and consent for their own mailbox.
/// Bound from the <c>Auth:Connectors</c> config section; secrets come from user-secrets (dev) or the
/// environment/secret store (prod).
/// </summary>
public sealed class ConnectorAuthOptions
{
    public const string SectionName = "Auth:Connectors";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
