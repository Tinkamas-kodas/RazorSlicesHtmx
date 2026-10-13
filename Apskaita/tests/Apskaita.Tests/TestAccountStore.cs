using Apskaita.Entities;
using Apskaita.Web.Features.Account;
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
    private readonly ApskaitaDbContext _db = db;

    public DbSet<User> Users => _db.Set<User>();

    public DbSet<CalendarConnector> Connectors => _db.Set<CalendarConnector>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
