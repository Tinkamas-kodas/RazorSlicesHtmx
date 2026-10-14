using Apskaita.Feature.TimeTracking.Persistence;
using Apskaita.Web.Data;
using Microsoft.EntityFrameworkCore;
using TimeTracking = Apskaita.Entities.TimeTracking;

namespace Apskaita.Web.Composition;

/// <summary>
/// Composition adapter exposing the shared <see cref="ApskaitaDbContext"/> as TimeTracking's private
/// write store <see cref="ITimeTrackingStore"/> (visible via <c>InternalsVisibleTo</c>).
/// </summary>
internal sealed class TimeTrackingStore(ApskaitaDbContext db) : ITimeTrackingStore
{
    public DbSet<TimeTracking> TimeTrackings => db.Set<TimeTracking>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
