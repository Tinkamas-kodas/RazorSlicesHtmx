using Apskaita.Entities;
using Apskaita.Feature.Admin.Persistence;
using Apskaita.Web.Data;
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
    public DbSet<Client> Clients => db.Set<Client>();

    public DbSet<Issuer> Issuers => db.Set<Issuer>();

    public DbSet<Unit> Units => db.Set<Unit>();

    public DbSet<Project> Projects => db.Set<Project>();

    public DbSet<InvoiceTemplate> InvoiceTemplates => db.Set<InvoiceTemplate>();

    public DbSet<SystemSettings> SystemSettings => db.Set<SystemSettings>();

    public Task<int> SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
