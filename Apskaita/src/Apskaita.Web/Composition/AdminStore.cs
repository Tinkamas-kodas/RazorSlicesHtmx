using Apskaita.Entities;
using Apskaita.Web.Features.Admin;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Composition;

/// <summary>
/// Composition-layer adapter implementing Admin's private <see cref="IAdminStore"/> over the shared
/// <see cref="ApskaitaDbContext"/>. Lives in <c>Apskaita.Web</c> (which holds <c>InternalsVisibleTo</c>
/// on the feature assembly) so the store interface stays <c>internal</c> to <c>Apskaita.Feature.Admin</c>.
/// Registered in <c>Setup.cs</c>.
/// </summary>
internal sealed class AdminStore(ApskaitaDbContext db) : IAdminStore
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
