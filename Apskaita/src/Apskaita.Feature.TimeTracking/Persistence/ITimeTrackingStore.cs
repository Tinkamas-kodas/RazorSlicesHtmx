using Microsoft.EntityFrameworkCore;
using TimeTrackingEntry = Apskaita.Entities.TimeTracking;

namespace Apskaita.Feature.TimeTracking.Persistence;

/// <summary>
/// TimeTracking's private write store over the shared <c>DbContext</c>: the <c>time_trackings</c>
/// table. Declared <c>internal</c> to this assembly (with <c>InternalsVisibleTo("Apskaita.Web")</c>)
/// so only TimeTracking's own endpoints can mutate these rows; other features read them through
/// <c>IQuery&lt;TEntity&gt;</c>. Implemented by the composition layer
/// (<c>Apskaita.Web.Composition.TimeTrackingStore</c>).
/// <para>
/// Unlike <c>IQuery&lt;TEntity&gt;</c> (read-only, no-tracking), the store exposes tracked <c>DbSet</c>s
/// for the owner's write path.
/// </para>
/// </summary>
internal interface ITimeTrackingStore
{
    DbSet<TimeTrackingEntry> TimeTrackings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
}
