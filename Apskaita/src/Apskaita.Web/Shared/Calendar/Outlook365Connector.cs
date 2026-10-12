using System.Net.Http.Headers;
using System.Text.Json;
using Apskaita.Entities;
using Microsoft.Extensions.Options;

namespace Apskaita.Web.Shared.Calendar;

/// <summary>
/// Outlook 365 connector: delegated Microsoft Graph access (SEC-004). Refreshes the cached access token
/// with the stored refresh token (REQ-005) and reads the signed-in user's week events via
/// <c>/me/calendarView</c>. All failures degrade to a notice + persisted <c>LastSyncError</c> (REQ-006);
/// nothing here throws toward the page. Provider metadata lives in <see cref="CalendarProviderInfo"/>.
/// </summary>
public sealed class Outlook365Connector(
    CalendarConnectorStore store,
    ConnectorTokenProtector protector,
    IHttpClientFactory httpClientFactory,
    IOptions<ConnectorAuthOptions> authOptions,
    ILogger logger) : ICalendarEventReader
{
    public const string ProviderKey = "outlook365";

    /// <summary>Delegated scopes requested on the connect consent (REQ-002).</summary>
    public const string ConnectScopes = "openid profile email https://graph.microsoft.com/Calendars.Read offline_access";

    private const int RefreshSkewSeconds = 300;

    public async Task<CalendarReadResult> ReadAsync(DateOnly fromLocal, DateOnly toLocal, CancellationToken ct)
    {
        var connectors = await store.GetOwnByProviderAsync(CalendarProvider.Outlook365, ct);
        if (connectors.Count == 0)
        {
            return new CalendarReadResult(false, [], null);
        }

        var events = new List<CalendarEventInfo>();
        string? notice = null;
        var failures = 0;

        // One connected mailbox failing must not blank the panel: read each connector independently (REQ-006).
        foreach (var connector in connectors)
        {
            try
            {
                var accessToken = await GetAccessTokenAsync(connector, ct);
                if (accessToken is null)
                {
                    failures++;
                    notice ??= "Calendar sign-in failed for one or more connected accounts. Reconnect them on the Account page.";
                    continue;
                }

                var connectorEvents = await QueryCalendarViewAsync(connector, accessToken, fromLocal, toLocal, ct);
                events.AddRange(connectorEvents);

                store.ApplySyncResult(connector, error: null);
                await store.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                failures++;
                var message = Shorten(ex is HttpRequestException http
                    ? $"Calendar unavailable: {(http.StatusCode.HasValue ? (int)http.StatusCode.Value : 0)}"
                    : "Calendar unavailable: " + ex.GetType().Name);

                store.ApplySyncResult(connector, message);
                await SafeSaveAsync(ct);

                logger.LogWarning("calendar sync failed for provider {Provider} account={Account}: {Message}", ProviderKey, connector.AccountEmail, message);
                notice ??= message;
            }
        }

        var merged = events
            .OrderBy(e => e.StartLocal)
            .ThenBy(e => e.Subject, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Success on some accounts with failures on others still shows events; the notice explains the gap.
        return new CalendarReadResult(true, merged, failures > 0 ? notice : null);
    }

    /// <summary>
    /// Returns a valid Graph access token: the cached one while it has more than
    /// <see cref="RefreshSkewSeconds"/> seconds of life left, otherwise refreshed with the stored refresh
    /// token and persisted (encrypted) on the connector row (REQ-005). Null when refresh fails.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CalendarConnector connector, CancellationToken ct)
    {
        var cached = protector.UnprotectOptional(connector.AccessTokenProtected);
        if (cached is not null
            && connector.AccessTokenExpiresAtUtc is { } expiry
            && expiry > DateTime.UtcNow.AddSeconds(RefreshSkewSeconds))
        {
            return cached;
        }

        var refreshToken = protector.UnprotectOptional(connector.RefreshTokenProtected);
        if (refreshToken is null)
        {
            store.ApplySyncResult(connector, "Stored refresh token could not be decrypted; reconnect required.");
            await SafeSaveAsync(ct);
            return null;
        }

        try
        {
            var response = await httpClientFactory.CreateClient("Outlook365Token").PostAsync(
                TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = authOptions.Value.ClientId,
                    ["client_secret"] = authOptions.Value.ClientSecret,
                    ["grant_type"] = "refresh_token",
                    ["refresh_token"] = refreshToken,
                    ["scope"] = "https://graph.microsoft.com/.default openid profile email offline_access",
                    ["requests_in_response_to_use_auth_microsoft_com"] = "true"
                }),
                ct);

            if (!response.IsSuccessStatusCode)
            {
                store.ApplySyncResult(connector, $"Token refresh failed (HTTP {(int)response.StatusCode}).");
                await SafeSaveAsync(ct);
                return null;
            }

            using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var root = payload.RootElement;
            var token = root.TryGetProperty("access_token", out var at) ? at.GetString() : null;
            var expiresIn = root.TryGetProperty("expires_in", out var ei) && ei.TryGetInt32(out var seconds)
                ? seconds
                : 3600;

            if (string.IsNullOrEmpty(token))
            {
                store.ApplySyncResult(connector, "Token refresh response had no access token.");
                await SafeSaveAsync(ct);
                return null;
            }

            // A rotating refresh token may be returned; store it when present.
            if (root.TryGetProperty("refresh_token", out var rt) && rt.GetString() is { } newRefresh && !string.IsNullOrEmpty(newRefresh))
            {
                connector.RefreshTokenProtected = protector.Protect(newRefresh);
            }

            connector.AccessTokenProtected = protector.ProtectOptional(token);
            connector.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(Math.Max(60, expiresIn - RefreshSkewSeconds));

            return token;
        }
        catch (Exception ex)
        {
            store.ApplySyncResult(connector, "Token refresh failed: " + ex.GetType().Name);
            await SafeSaveAsync(ct);
            return null;
        }
    }

    private async Task<IReadOnlyList<CalendarEventInfo>> QueryCalendarViewAsync(
        CalendarConnector connector,
        string accessToken,
        DateOnly fromLocal,
        DateOnly toLocal,
        CancellationToken ct)
    {
        // UTC boundaries with a one-day margin on both sides so any local-timezone offset (and DST) is
        // covered; events are filtered to the local range after conversion (PAT-003).
        var startUtc = fromLocal.ToDateTime(TimeOnly.MinValue).AddDays(-1).ToUniversalTime();
        var endUtc = toLocal.ToDateTime(TimeOnly.MaxValue).AddDays(1).ToUniversalTime();

        var url = $"https://graph.microsoft.com/v1.0/me/calendarView" +
            $"?startDateTime={startUtc:yyyy-MM-ddTHH:mm:ssZ}Z" +
            $"&endDateTime={endUtc:yyyy-MM-ddTHH:mm:ssZ}Z" +
            "&$select=subject,isOnlineMeeting,locations,organizer,start,end" +
            "&$orderby=start%2FdateTime%20asc" +
            "&$top=200";

        var client = httpClientFactory.CreateClient("Outlook365Graph");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await client.GetAsync(url, ct);

        if (response.StatusCode is System.Net.HttpStatusCode.Unauthorized)
        {
            // Force a refresh on the next attempt; the cached token is dead.
            connector.AccessTokenProtected = null;
            connector.AccessTokenExpiresAtUtc = null;
            throw new InvalidOperationException("Graph rejected the cached access token (401).");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Graph calendarView failed (HTTP {(int)response.StatusCode}).");
        }

        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var result = new List<CalendarEventInfo>();

        if (!payload.RootElement.TryGetProperty("value", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var item in items.EnumerateArray())
        {
            var start = ParseDateTimeProperty(item, "start");
            var end = ParseDateTimeProperty(item, "end");
            if (start is null || end is null)
            {
                continue;
            }

            var startLocal = start.Value.LocalDateTime;
            var endLocal = end.Value.LocalDateTime;
            if (startLocal < fromLocal.ToDateTime(TimeOnly.MinValue) || startLocal >= toLocal.ToDateTime(TimeOnly.MaxValue).AddDays(1))
            {
                continue;
            }

            var subject = item.TryGetProperty("subject", out var s) && s.ValueKind == JsonValueKind.String
                ? s.GetString()
                : null;

            var isOnline = item.TryGetProperty("isOnlineMeeting", out var om) && om.ValueKind == JsonValueKind.True;

            string? location = null;
            if (item.TryGetProperty("locations", out var locs) && locs.ValueKind == JsonValueKind.Array)
            {
                foreach (var loc in locs.EnumerateArray())
                {
                    if (loc.TryGetProperty("displayName", out var dn) && dn.ValueKind == JsonValueKind.String)
                    {
                        location = dn.GetString();
                        break;
                    }
                }
            }

            string? organizer = null;
            if (item.TryGetProperty("organizer", out var org) && org.ValueKind == JsonValueKind.Object)
            {
                if (org.TryGetProperty("emailAddress", out var addr) && addr.ValueKind == JsonValueKind.Object
                    && addr.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String)
                {
                    organizer = name.GetString();
                }
            }

            result.Add(new CalendarEventInfo(
                string.IsNullOrWhiteSpace(subject) ? "(No subject)" : subject,
                startLocal,
                endLocal,
                location,
                isOnline,
                organizer));
        }

        return result;
    }

    /// <summary>Parses a Graph <c>{ dateTime, timeZone }</c> property into a <see cref="DateTimeOffset"/>; null when absent.</summary>
    private static DateTimeOffset? ParseDateTimeProperty(JsonElement item, string name)
    {
        if (!item.TryGetProperty(name, out var prop) || prop.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!prop.TryGetProperty("dateTime", out var dt) || dt.ValueKind != JsonValueKind.String
            || !DateTimeOffset.TryParse(dt.GetString(), out var parsed))
        {
            return null;
        }

        // Graph already returns an absolute offset; "UTC" needs no adjustment, and named zones in the
        // property are advisory for the stored offset. Convert to the local display zone downstream.
        return parsed.ToUniversalTime();
    }

    /// <summary>Multi-tenant token endpoint of the dedicated connector app (common authority).</summary>
    private const string TokenEndpoint = "https://login.microsoftonline.com/common/oauth2/v2.0/token";

    private async Task SafeSaveAsync(CancellationToken ct)
    {
        try
        {
            await store.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning("calendar connector metadata could not be saved: {Error}", ex.GetType().Name);
        }
    }

    private static string Shorten(string message) =>
        message.Length > 200 ? message[..200] : message;
}
