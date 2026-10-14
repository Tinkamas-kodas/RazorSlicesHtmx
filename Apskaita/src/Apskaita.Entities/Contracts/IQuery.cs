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
/// </summary>
public interface IQuery<out TEntity> where TEntity : class
{
    /// <summary>Returns a composable, no-tracking <see cref="IQueryable{T}"/> over the table.</summary>
    IQueryable<TEntity> All();
}
