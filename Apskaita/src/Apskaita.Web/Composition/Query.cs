using Apskaita.Entities;
using Apskaita.Entities.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Composition;

/// <summary>
/// The single open-generic <see cref="IQuery{TEntity}"/> implementation over the shared
/// <see cref="ApskaitaDbContext"/>. Registered as <c>typeof(IQuery&lt;&gt;)</c> so any feature can
/// inject <c>IQuery&lt;TEntity&gt;</c> for any table without per-entity plumbing.
/// <para>
/// <see cref="All"/> applies <c>AsNoTracking</c> by contract: reads crossing a feature line must never
/// be tracked, otherwise a consumer mutating a loaded entity would have the change persisted by the
/// owning feature's next <c>SaveChanges</c> on the same context.
/// </para>
/// </summary>
public sealed class Query<TEntity>(ApskaitaDbContext db) : IQuery<TEntity> where TEntity : class
{
    public IQueryable<TEntity> All() => db.Set<TEntity>().AsNoTracking();
}
