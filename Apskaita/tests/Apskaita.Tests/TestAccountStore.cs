using Apskaita.Entities;
using Apskaita.Feature.Account.Persistence;
using Apskaita.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Tests;

/// <summary>
/// Test adapter implementing Account's internal <c>IAccountStore</c> over a shared EF In-Memory
/// <see cref="ApskaitaDbContext"/>. Lets unit tests construct the feature's internal services
/// (<c>AccountProvisioner</c>, <c>CalendarConnectorStore</c>) and assert against the same context's
/// local tracker (<c>db.Users.Local</c> / <c>db.CalendarConnectors.Local</c>), mirroring how the
/// composition layer wires the real store.
/// </summary>
internal sealed class TestAccountStore(ApskaitaDbContext db) : IAccountStore
{
    public DbSet<User> Users => db.Set<User>();

    public DbSet<CalendarConnector> Connectors => db.Set<CalendarConnector>();

    public DbSet<CalendarImportedEvent> ImportedEvents => db.Set<CalendarImportedEvent>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
