using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin;

/// <summary>
/// Admin's private write store over the shared <c>DbContext</c>: the classifier/registry tables
/// (<c>clients</c>, <c>issuers</c>, <c>units</c>, <c>projects</c>, <c>invoice_templates</c>,
/// <c>system_settings</c>). Declared <c>internal</c> to this assembly (with
/// <c>InternalsVisibleTo("Apskaita.Web")</c>) so only Admin's own registries can mutate these tables;
/// other features read them through <c>IQuery&lt;TEntity&gt;</c>. Implemented by the composition layer
/// (<c>Apskaita.Web.Composition.AdminStore</c>).
/// <para>
/// The <c>users</c> table is owned by <c>Apskaita.Feature.Account</c> (its <c>IAccountStore</c>), not here.
/// </para>
/// </summary>
internal interface IAdminStore
{
    DbSet<Client> Clients { get; }

    DbSet<Issuer> Issuers { get; }

    DbSet<Unit> Units { get; }

    DbSet<Project> Projects { get; }

    DbSet<InvoiceTemplate> InvoiceTemplates { get; }

    DbSet<SystemSettings> SystemSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
}
