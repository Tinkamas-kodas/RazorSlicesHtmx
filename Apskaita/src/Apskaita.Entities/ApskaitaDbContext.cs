using Apskaita.Entities.Mapping;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Entities;

public sealed class ApskaitaDbContext(DbContextOptions<ApskaitaDbContext> options) : DbContext(options)
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
    public DbSet<WorkAct> WorkActs => Set<WorkAct>();
    public DbSet<WorkActLine> WorkActLines => Set<WorkActLine>();
    public DbSet<TimeTracking> TimeTrackings => Set<TimeTracking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyApskaitaEntityMappings();
    }
}