using Apskaita.Entities;
using Apskaita.Feature.WorkActs.Persistence;
using Apskaita.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Composition;

/// <summary>
/// Composition adapter exposing the shared <see cref="ApskaitaDbContext"/> as WorkActs' private write
/// store <see cref="IWorkActStore"/> (visible via <c>InternalsVisibleTo</c>).
/// </summary>
internal sealed class WorkActStore(ApskaitaDbContext db) : IWorkActStore
{
    public DbSet<WorkAct> WorkActs => db.Set<WorkAct>();

    public DbSet<WorkActLine> WorkActLines => db.Set<WorkActLine>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
