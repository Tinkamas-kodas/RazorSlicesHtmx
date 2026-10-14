using System.Linq.Expressions;
using Apskaita.Entities.Contracts;
using Apskaita.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Tests;

/// <summary>
/// Minimal <see cref="IQuery{T}"/> over an <see cref="ApskaitaDbContext"/> for unit tests that read
/// through the cross-feature seam — no-tracking, like the composition implementation in Web.
/// </summary>
public sealed class TestQuery<T>(ApskaitaDbContext db) : IQuery<T>
    where T : class
{
    public IQueryable<T> All() => db.Set<T>().AsNoTracking();

    public Task<bool> ExistsAsync(Expression<Func<T, bool>> predicate, CancellationToken ct) =>
        db.Set<T>().AsNoTracking().AnyAsync(predicate, ct);
}
