using Apskaita.Entities;
using Apskaita.Feature.Admin.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Tests;

/// <summary>
/// Test adapter implementing Admin's internal <c>IAdminStore</c> over a shared EF In-Memory
/// <see cref="ApskaitaDbContext"/>. Lets unit tests construct the registries' content services and
/// assert against the same context's tracker, mirroring how the composition layer wires the real store.
/// </summary>
internal sealed class TestAdminStore(ApskaitaDbContext db) : IAdminStore
{
    public DbSet<Client> Clients => db.Set<Client>();

    public DbSet<Issuer> Issuers => db.Set<Issuer>();

    public DbSet<Unit> Units => db.Set<Unit>();

    public DbSet<Project> Projects => db.Set<Project>();

    public DbSet<InvoiceTemplate> InvoiceTemplates => db.Set<InvoiceTemplate>();

    public DbSet<SystemSettings> SystemSettings => db.Set<SystemSettings>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
