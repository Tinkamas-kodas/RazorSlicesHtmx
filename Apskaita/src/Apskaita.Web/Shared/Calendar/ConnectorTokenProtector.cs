using Microsoft.AspNetCore.DataProtection;

namespace Apskaita.Web.Shared.Calendar;

/// <summary>
/// Protects calendar OAuth tokens at rest with ASP.NET Core Data Protection (SEC-001). Tokens are stored
/// on <c>calendar_connectors</c> only in protected form; plaintext never touches the database or logs.
/// </summary>
public sealed class ConnectorTokenProtector(IDataProtectionProvider provider)
{
    private const string Purpose = "Apskaita.CalendarConnector";

    private readonly Lazy<IDataProtector> _protector = new(() => provider.CreateProtector(Purpose));

    /// <summary>Protects a token for storage; null/empty stays null (for optional tokens).</summary>
    public string? ProtectOptional(string? token) =>
        string.IsNullOrEmpty(token) ? null : _protector.Value.Protect(token);

    /// <summary>Protects a required token for storage.</summary>
    public string Protect(string token) => _protector.Value.Protect(token);

    /// <summary>Unprotects a stored token; returns null when the payload is missing or cannot be decrypted.</summary>
    public string? UnprotectOptional(string? protectedToken)
    {
        if (string.IsNullOrEmpty(protectedToken))
        {
            return null;
        }

        try
        {
            return _protector.Value.Unprotect(protectedToken);
        }
        catch (Exception)
        {
            // Key rotation/corruption: treat as no token; the connector will re-sync or require reconnect.
            return null;
        }
    }
}
