using Apskaita.Entities;
using Apskaita.Web.Features.Account;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Composition;

/// <summary>
/// Composition-layer adapter implementing Account's private <see cref="IAccountStore"/> over the shared
/// <see cref="ApskaitaDbContext"/>. Lives in <c>Apskaita.Web</c> (which holds
/// <c>InternalsVisibleTo</c> on the feature assembly) so the store interface stays <c>internal</c> to
/// <c>Apskaita.Feature.Account</c>: only Account's services and this adapter can reach the tracked
/// <c>DbSet</c>s. Registered in <c>Setup.cs</c>.
/// </summary>
internal sealed class AccountStore(ApskaitaDbContext db) : IAccountStore
{
    public DbSet<User> Users => db.Set<User>();

    public DbSet<CalendarConnector> Connectors => db.Set<CalendarConnector>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
