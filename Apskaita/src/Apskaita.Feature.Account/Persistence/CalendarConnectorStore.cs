using System.Security.Claims;
using Apskaita.Web.Features.Account;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Shared.Calendar;

/// <summary>
/// Per-user connector persistence (SEC-002): the owner is always resolved from the session principal's
/// user-id claim (<see cref="AuthConstants.UserIdClaim"/>, issued at sign-in by <c>AccountProvisioner</c>),
/// never from a route or form value. Used by the account endpoints, the connect callback, and the Graph
/// connector for token-cache updates.
/// </summary>
internal sealed class CalendarConnectorStore(IAccountStore store, IHttpContextAccessor httpContextAccessor) : ICalendarConnectorTokenStore
{
    /// <summary>Normalized email of the current session user, or null when unauthenticated.</summary>
    public string? CurrentEmail
    {
        get
        {
            var name = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value;
            return string.IsNullOrWhiteSpace(name) ? null : name.Trim().ToLowerInvariant();
        }
    }

    /// <summary>
    /// Id of the current session user from the <c>userId</c> claim — no Users-table lookup needed. Null when
    /// unauthenticated or when the session predates the claim (re-sign-in required).
    /// </summary>
    public Guid? CurrentUserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(AuthConstants.UserIdClaim)?.Value, out var id)
            ? id
            : null;

    public async Task<CalendarConnector?> GetOwnAsync(CalendarProvider provider, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return null;
        }

        return await store.Connectors
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider, ct);
    }

    public async Task<IReadOnlyList<CalendarConnector>> GetOwnAllAsync(CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return [];
        }

        return await store.Connectors
            .Where(c => c.UserId == userId)
            .OrderBy(c => c.Provider)
            .ToListAsync(ct);
    }

    /// <summary>All connectors of the current user for one provider (multi-account per provider).</summary>
    public async Task<IReadOnlyList<CalendarConnector>> GetOwnByProviderAsync(CalendarProvider provider, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return [];
        }

        return await store.Connectors
            .Where(c => c.UserId == userId && c.Provider == provider)
            .OrderBy(c => c.AccountEmail)
            .ToListAsync(ct);
    }

    /// <summary>
    /// Creates or replaces the current user's connector for one account of a provider (REQ-002). Reconnecting
    /// the same mailbox updates the existing row; a different mailbox adds a new one. Returns null when the
    /// session carries no user-id claim.
    /// </summary>
    public async Task<CalendarConnector?> UpsertOwnAsync(
        CalendarProvider provider,
        string accountKey,
        string accountEmail,
        string displayName,
        string refreshTokenProtected,
        string scopes,
        CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return null;
        }

        var now = DateTime.UtcNow;
        var connector = await store.Connectors
            .FirstOrDefaultAsync(c => c.UserId == userId && c.Provider == provider && c.AccountKey == accountKey, ct);

        if (connector is null)
        {
            connector = new CalendarConnector
            {
                Id = Guid.NewGuid(),
                UserId = userId.Value,
                Provider = provider,
                AccountKey = accountKey,
                CreatedAtUtc = now,
                CreatedByUserId = userId.Value
            };
            store.Connectors.Add(connector);
        }

        connector.AccountEmail = accountEmail;
        connector.DisplayName = displayName;
        connector.RefreshTokenProtected = refreshTokenProtected;
        connector.Scopes = scopes;
        connector.ConnectedAtUtc = now;
        connector.AccessTokenProtected = null;
        connector.AccessTokenExpiresAtUtc = null;
        connector.LastSyncAtUtc = null;
        connector.LastSyncError = null;
        connector.UpdatedAtUtc = now;
        connector.UpdatedByUserId = userId.Value;

        await store.SaveChangesAsync(ct);
        return connector;
    }

    /// <summary>Deletes the current user's connector for a provider (REQ-003); false when none existed.</summary>
    public async Task<bool> DisconnectOwnAsync(CalendarProvider provider, CancellationToken ct)
    {
        var connector = await GetOwnAsync(provider, ct);
        if (connector is null)
        {
            return false;
        }

        store.Connectors.Remove(connector);
        await store.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Deletes one of the current user's connectors by id (per-account disconnect, REQ-003). Ownership is
    /// verified against the session user; returns the provider for the response, or null when not owned/not found.
    /// </summary>
    public async Task<CalendarProvider?> DisconnectByIdAsync(Guid id, CancellationToken ct)
    {
        var userId = CurrentUserId;
        if (userId is null)
        {
            return null;
        }

        var connector = await store.Connectors
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId, ct);

        if (connector is null)
        {
            return null;
        }

        var provider = connector.Provider;
        store.Connectors.Remove(connector);
        await store.SaveChangesAsync(ct);
        return provider;
    }

    /// <summary>Persists token-cache and sync-metadata changes made by the Graph connector.</summary>
    public async Task SaveChangesAsync(CancellationToken ct) => await store.SaveChangesAsync(ct);

    /// <summary>
    /// Records the outcome of a sync attempt on a tracked connector row: success clears the error and stamps
    /// <c>LastSyncAtUtc</c>; failure stores a truncated message. Callers must invoke <see cref="SaveChangesAsync"/>.
    /// </summary>
    public void ApplySyncResult(CalendarConnector connector, string? error)
    {
        var now = DateTime.UtcNow;
        if (error is null)
        {
            connector.LastSyncAtUtc = now;
            connector.LastSyncError = null;
        }
        else
        {
            connector.LastSyncError = error.Length > FieldLengths.CalendarConnector.LastSyncError
                ? error[..FieldLengths.CalendarConnector.LastSyncError]
                : error;
        }

        connector.UpdatedAtUtc = now;
    }
}
