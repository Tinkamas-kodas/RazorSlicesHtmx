using System.Linq.Expressions;

namespace Apskaita.Entities.Contracts;

/// <summary>
/// Read-only, composable access to a table. This is the ONLY cross-feature data-access surface:
/// a feature injects <c>IQuery&lt;Project&gt;</c>, <c>IQuery&lt;User&gt;</c>, ... and composes LINQ
/// (<c>Where</c>/<c>OrderBy</c>/<c>Select</c>/<c>Skip</c>/<c>Take</c>) directly against the entity,
/// which EF translates to a single SQL query.
/// <para>
/// The contract is deliberately read-only — it exposes no <c>DbSet</c>, so no mutation, attach, or
/// delete. Write capability lives in the owning feature's private store interface, never here.
/// </para>
/// <para>
/// Implementations MUST return a no-tracking query: with one shared <c>DbContext</c>, a tracked entity
/// handed across a feature line could be silently persisted by the owning feature's next
/// <c>SaveChanges</c>. See <c>Apskaita.Web.Composition.Query&lt;&gt;</c>.
/// </para>
/// <para>
/// The interface is invariant (not <c>out TEntity</c>) because <see cref="ExistsAsync"/> takes a
/// predicate over <typeparamref name="TEntity"/> — an input position.
/// </para>
/// </summary>
public interface IQuery<TEntity> where TEntity : class
{
    /// <summary>Returns a composable, no-tracking <see cref="IQueryable{T}"/> over the table.</summary>
    IQueryable<TEntity> All();

    /// <summary>
    /// True when at least one row matches <paramref name="predicate"/>, evaluated server-side (one
    /// <c>SELECT 1 ... LIMIT</c> round trip, no entities materialized). Use it instead of
    /// <c>All().AnyAsync(...)</c> so callers need no database-provider dependency.
    /// </summary>
    Task<bool> ExistsAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct);

    /// <summary>
    /// The first no-tracking entity matching <paramref name="predicate"/>, or <c>null</c>. The read-seam
    /// companion to <see cref="ExistsAsync"/>: use it instead of <c>All().FirstOrDefaultAsync(...)</c> so a
    /// pure read needs no database-provider dependency (the by-id pattern is <c>e =&gt; e.Id == id</c>).
    /// </summary>
    Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct);
}
