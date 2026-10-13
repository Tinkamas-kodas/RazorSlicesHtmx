using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Account;

/// <summary>
/// Account's private write store over the shared <c>DbContext</c>: the <c>users</c> and
/// <c>calendar_connectors</c> tables. Declared <c>internal</c> to this assembly (with
/// <c>InternalsVisibleTo("Apskaita.Web")</c>) so only Account's own services can mutate these tables;
/// other features read them through <c>IQuery&lt;TEntity&gt;</c>. Implemented by the composition layer
/// (<c>Apskaita.Web.Composition.AccountStore</c>).
/// <para>
/// Unlike <c>IQuery&lt;TEntity&gt;</c> (read-only, no-tracking), the store exposes tracked <c>DbSet</c>s
/// for the owner's write path.
/// </para>
/// </summary>
internal interface IAccountStore
{
    DbSet<User> Users { get; }

    DbSet<CalendarConnector> Connectors { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
}
