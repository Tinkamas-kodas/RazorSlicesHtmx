using Apskaita.Entities;
using Apskaita.Web.Features.Admin;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Tests;

/// <summary>
/// Test adapter implementing Admin's internal <c>IAdminStore</c> over a shared EF In-Memory
/// <see cref="ApskaitaDbContext"/>. Lets unit tests construct the registries' content services and
/// assert against the same context's tracker, mirroring how the composition layer wires the real store.
/// </summary>
internal sealed class TestAdminStore(ApskaitaDbContext db) : IAdminStore
{
    private readonly ApskaitaDbContext _db = db;

    public DbSet<Client> Clients => _db.Set<Client>();

    public DbSet<Issuer> Issuers => _db.Set<Issuer>();

    public DbSet<Unit> Units => _db.Set<Unit>();

    public DbSet<Project> Projects => _db.Set<Project>();

    public DbSet<InvoiceTemplate> InvoiceTemplates => _db.Set<InvoiceTemplate>();

    public DbSet<SystemSettings> SystemSettings => _db.Set<SystemSettings>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
