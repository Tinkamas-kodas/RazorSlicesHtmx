using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apskaita.Entities.Mapping;

/// <summary>
/// Entity property constraints (max lengths, precision, column types). Table, column, and index names are
/// derived from entity/property names by <c>EFCore.NamingConventions</c> (snake case) — see
/// <c>UseSnakeCaseNamingConvention()</c> in <see cref="DataAccessServiceCollectionExtensions"/> — so nothing
/// here names database objects explicitly. Enum-backed properties use native MySQL <c>enum</c> columns via
/// <see cref="MySqlEnumTypeExtensions.ToMySqlDefinition{T}"/> combined with <c>HasConversion&lt;string&gt;()</c>.
/// </summary>
public static class ApskaitaEntityModelBuilderExtensions
{
    public static void ApplyApskaitaEntityMappings(this ModelBuilder modelBuilder)
    {
        ConfigureUser(modelBuilder);
        ConfigureCompany(modelBuilder);
        ConfigureInvoiceTemplate(modelBuilder);
        ConfigureProject(modelBuilder);
        ConfigureWorkAct(modelBuilder);
        ConfigureWorkActLine(modelBuilder);
        ConfigureTimeTracking(modelBuilder);
        ConfigureUnit(modelBuilder);
        ConfigureInvoice(modelBuilder);
        ConfigureInvoiceLine(modelBuilder);
        ConfigureSystemSettings(modelBuilder);
        ConfigureInvoiceSequenceState(modelBuilder);
    }

    private static void ConfigureUser(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.Property(x => x.SubjectId).HasMaxLength(FieldLengths.User.SubjectId).IsRequired();
            entity.Property(x => x.Provider).HasMaxLength(FieldLengths.User.Provider).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(FieldLengths.User.Email).IsRequired();
            entity.Property(x => x.DisplayName).HasMaxLength(FieldLengths.User.DisplayName).IsRequired();
            entity.Property(x => x.Role)
                .HasColumnType(UserRole.Admin.ToMySqlDefinition())
                .HasConversion<string>();
            entity.HasIndex(x => x.Email).IsUnique();
        });
    }

    private static void ConfigureCompany(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.IsActive);

            entity.HasDiscriminator()
                .HasValue<Company>(nameof(Company))
                .HasValue<Client>(nameof(Client))
                .HasValue<Issuer>(nameof(Issuer));

            entity.OwnsOne(x => x.Info, ConfigureCompanyInfo);
        });
    }

    private static void ConfigureInvoiceTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceTemplate>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(FieldLengths.InvoiceTemplate.Code).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(FieldLengths.InvoiceTemplate.Name).IsRequired();
            entity.Property(x => x.LanguageCode).HasMaxLength(FieldLengths.InvoiceTemplate.LanguageCode).IsRequired();
            entity.Property(x => x.Content).HasMaxLength(20000).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
        });
    }

    private static void ConfigureProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.Property(x => x.Name).HasMaxLength(FieldLengths.Project.Name).IsRequired();
            entity.Property(x => x.Code).HasMaxLength(FieldLengths.Project.Code);
            entity.Property(x => x.ColorRgb).HasMaxLength(FieldLengths.Project.ColorRgb).IsRequired().HasDefaultValue("#FF0000");
            entity.Property(x => x.CurrencyCode).HasMaxLength(FieldLengths.Project.CurrencyCode).IsRequired();
            entity.HasIndex(x => new { x.ClientId, x.IsActive });
        });
    }

    private static void ConfigureWorkAct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkAct>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(FieldLengths.WorkAct.Number).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(FieldLengths.WorkAct.CurrencyCode).IsRequired();
            entity.Property(x => x.TotalHours).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.Status)
                .HasColumnType(WorkActStatus.Draft.ToMySqlDefinition())
                .HasConversion<string>();
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => new { x.ProjectId, x.Status });
        });
    }

    private static void ConfigureWorkActLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkActLine>(entity =>
        {
            entity.Property(x => x.ExternalId).HasMaxLength(FieldLengths.WorkActLine.ExternalId);
            entity.Property(x => x.Source)
                .HasColumnType(WorkActLineSource.Clockify.ToMySqlDefinition())
                .HasConversion<string>();
            entity.Property(x => x.Description).HasMaxLength(FieldLengths.WorkActLine.Description).IsRequired();
            entity.Property(x => x.WorkDate).HasColumnType("date");
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.WorkActId, x.WorkDate });
        });
    }

    private static void ConfigureTimeTracking(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeTracking>(entity =>
        {
            entity.Property(x => x.HourlyRate).IsRequired(false);
            entity.Property(x => x.Description).HasMaxLength(FieldLengths.TimeTracking.Description).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.StartedAtLocal });
        });
    }

    private static void ConfigureUnit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Unit>(entity =>
        {
            entity.Property(x => x.Code).HasMaxLength(FieldLengths.Unit.Code).IsRequired();
            entity.Property(x => x.Name).HasMaxLength(FieldLengths.Unit.Name).IsRequired();
            entity.Property(x => x.IsActive).HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique();
        });
    }

    private static void ConfigureInvoice(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.Property(x => x.Number).HasMaxLength(FieldLengths.Invoice.Number).IsRequired();
            entity.Property(x => x.CurrencyCode).HasMaxLength(FieldLengths.Invoice.CurrencyCode).IsRequired();
            entity.Property(x => x.LanguageCode).HasMaxLength(FieldLengths.Invoice.LanguageCode).IsRequired();
            entity.Property(x => x.IssueDate).HasColumnType("date");
            entity.Property(x => x.DueDate).HasColumnType("date");
            entity.Property(x => x.SubtotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.VatAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmountInText).HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Status)
                .HasColumnType(InvoiceStatus.Draft.ToMySqlDefinition())
                .HasConversion<string>();
            entity.Property(x => x.Notes).HasMaxLength(1000);
            entity.HasIndex(x => x.Number).IsUnique();
            entity.HasIndex(x => x.SequenceNumber).IsUnique();
            entity.HasIndex(x => new { x.IssuerId, x.IssueDate });
            entity.HasIndex(x => new { x.ClientId, x.IssueDate });

            entity.OwnsOne(x => x.IssuerInfo, ConfigureCompanyInfo);
            entity.OwnsOne(x => x.ClientInfo, ConfigureCompanyInfo);
        });
    }

    private static void ConfigureInvoiceLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.Property(x => x.SourceType)
                .HasColumnType(InvoiceLineSourceType.WorkAct.ToMySqlDefinition())
                .HasConversion<string>();
            entity.Property(x => x.Description).HasMaxLength(FieldLengths.InvoiceLine.Description).IsRequired();
            entity.Property(x => x.Quantity).HasPrecision(18, 2);
            entity.Property(x => x.UnitPrice).HasPrecision(18, 4);
            entity.Property(x => x.Amount).HasPrecision(18, 2);
            entity.Property(x => x.VatRatePercent).HasPrecision(5, 2);
            entity.Property(x => x.VatAmount).HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasPrecision(18, 2);
            entity.HasIndex(x => new { x.InvoiceId, x.LineNo }).IsUnique();
            entity.HasIndex(x => x.WorkActId).IsUnique();
        });
    }

    private static void ConfigureSystemSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.Property(x => x.DefaultVatRatePercent).HasPrecision(5, 2);
            entity.Property(x => x.InvoiceNumberTemplate).HasMaxLength(FieldLengths.SystemSettings.InvoiceNumberTemplate).IsRequired();
        });
    }

    private static void ConfigureInvoiceSequenceState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceSequenceState>(entity =>
        {
            entity.Property(x => x.PrefixContext).HasMaxLength(FieldLengths.InvoiceSequenceState.PrefixContext);
            entity.Property(x => x.ResetRule)
                .HasColumnType(ResetRule.None.ToMySqlDefinition())
                .HasConversion<string>();
        });
    }

    private static void ConfigureCompanyInfo<TOwner>(OwnedNavigationBuilder<TOwner, CompanyInfo> owned)
        where TOwner : class
    {
        owned.Property(x => x.Name).HasMaxLength(FieldLengths.CompanyInfo.Name).IsRequired();
        owned.Property(x => x.CompanyCode).HasMaxLength(FieldLengths.CompanyInfo.CompanyCode);
        owned.Property(x => x.VatCode).HasMaxLength(FieldLengths.CompanyInfo.VatCode);
        owned.Property(x => x.AddressLine1).HasMaxLength(FieldLengths.CompanyInfo.AddressLine1);
        owned.Property(x => x.AddressLine2).HasMaxLength(FieldLengths.CompanyInfo.AddressLine2);
        owned.Property(x => x.City).HasMaxLength(FieldLengths.CompanyInfo.City);
        owned.Property(x => x.PostalCode).HasMaxLength(FieldLengths.CompanyInfo.PostalCode);
        owned.Property(x => x.CountryCode).HasMaxLength(FieldLengths.CompanyInfo.CountryCode);
        owned.Property(x => x.ContactName).HasMaxLength(FieldLengths.CompanyInfo.ContactName);
        owned.Property(x => x.ContactEmail).HasMaxLength(FieldLengths.CompanyInfo.ContactEmail);
        owned.Property(x => x.ContactPhone).HasMaxLength(FieldLengths.CompanyInfo.ContactPhone);
        owned.Property(x => x.BankName).HasMaxLength(FieldLengths.CompanyInfo.BankName);
        owned.Property(x => x.BankAccount).HasMaxLength(FieldLengths.CompanyInfo.BankAccount);
        owned.Property(x => x.BankSwift).HasMaxLength(FieldLengths.CompanyInfo.BankSwift);
        owned.Property(x => x.VatPayerCode).HasMaxLength(FieldLengths.CompanyInfo.VatPayerCode);
    }
}
