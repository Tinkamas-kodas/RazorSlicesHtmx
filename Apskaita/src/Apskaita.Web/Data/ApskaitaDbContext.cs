using System.Diagnostics.CodeAnalysis;
using Apskaita.Entities;
using Apskaita.Entities.Mapping;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Data;

/// <summary>
/// Application database context. Also implements <see cref="IDataProtectionKeyContext"/> so the ASP.NET
/// Core Data Protection key ring persists to the database (<c>PersistKeysToDbContext</c>) instead of the
/// local machine — calendar connector tokens survive redeployment and scale-out (see README).
/// Table/column names follow the snake_case naming convention like the rest of the schema.
/// </summary>
[SuppressMessage("ReSharper", "UnusedMember.Global")]
public sealed class ApskaitaDbContext(DbContextOptions<ApskaitaDbContext> options) : DbContext(options), IDataProtectionKeyContext
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Issuer> Issuers => Set<Issuer>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    public DbSet<InvoiceSequenceState> InvoiceSequenceStates => Set<InvoiceSequenceState>();
    public DbSet<InvoiceTemplate> InvoiceTemplates => Set<InvoiceTemplate>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<SystemSettings> SystemSettings => Set<SystemSettings>();
    public DbSet<Unit> Units => Set<Unit>();
    public DbSet<User> Users => Set<User>();
    public DbSet<WorkAct> WorkActs => Set<WorkAct>();
    public DbSet<WorkActLine> WorkActLines => Set<WorkActLine>();
    public DbSet<TimeTracking> TimeTrackings => Set<TimeTracking>();
    public DbSet<CalendarConnector> CalendarConnectors => Set<CalendarConnector>();

    /// <summary>Data Protection key ring store entity (required by <c>PersistKeysToDbContext</c>).</summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyApskaitaEntityMappings();
    }
}