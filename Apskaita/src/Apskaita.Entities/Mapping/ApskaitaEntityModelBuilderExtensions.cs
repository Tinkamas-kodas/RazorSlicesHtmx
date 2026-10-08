using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Apskaita.Entities.Mapping;

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
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.EntraObjectId).HasColumnName("entra_object_id").HasMaxLength(FieldLengths.User.EntraObjectId).IsRequired();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasMaxLength(FieldLengths.User.DisplayName).IsRequired();
            entity.Property(x => x.Email).HasColumnName("email").HasMaxLength(FieldLengths.User.Email);
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
            entity.Property(x => x.LastLoginAtUtc).HasColumnName("last_login_at_utc");
            entity.HasIndex(x => x.EntraObjectId).IsUnique().HasDatabaseName("ux_users_entra_object_id");
        });
    }

    private static void ConfigureCompany(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(entity =>
        {
            entity.ToTable("companies");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.HasIndex(x => x.IsActive).HasDatabaseName("ix_companies_is_active");

            entity.HasDiscriminator<string>("company_type")
                .HasValue<Company>(nameof(Company))
                .HasValue<Client>(nameof(Client))
                .HasValue<Issuer>(nameof(Issuer));

            entity.OwnsOne(x => x.Info, owned => ConfigureCompanyInfo(owned, "info"));
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureInvoiceTemplate(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceTemplate>(entity =>
        {
            entity.ToTable("invoice_templates");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(FieldLengths.InvoiceTemplate.Code).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(FieldLengths.InvoiceTemplate.Name).IsRequired();
            entity.Property(x => x.LanguageCode).HasColumnName("language_code").HasMaxLength(FieldLengths.InvoiceTemplate.LanguageCode).IsRequired();
            entity.Property(x => x.Content).HasColumnName("content").HasMaxLength(20000).IsRequired();
            entity.Property(x => x.IsDefault).HasColumnName("is_default");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_invoice_templates_code");
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureProject(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ClientId).HasColumnName("client_id");
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(FieldLengths.Project.Name).IsRequired();
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(FieldLengths.Project.Code);
            entity.Property(x => x.HourlyRate).HasColumnName("hourly_rate");
            entity.Property(x => x.ColorRgb).HasColumnName("color_rgb").HasMaxLength(FieldLengths.Project.ColorRgb).IsRequired().HasDefaultValue("#FF0000");
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(FieldLengths.Project.CurrencyCode).IsRequired();
            entity.Property(x => x.InvoiceTemplateId).HasColumnName("invoice_template_id");
            entity.Property(x => x.IsActive).HasColumnName("is_active");
            entity.HasIndex(x => new { x.ClientId, x.IsActive }).HasDatabaseName("ix_projects_client_id_is_active");
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureWorkAct(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkAct>(entity =>
        {
            entity.ToTable("work_acts");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Number).HasColumnName("number").HasMaxLength(FieldLengths.WorkAct.Number).IsRequired();
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.ClientId).HasColumnName("client_id");
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(FieldLengths.WorkAct.CurrencyCode).IsRequired();
            entity.Property(x => x.TotalHours).HasColumnName("total_hours").HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
            entity.Property(x => x.InvoicedAtUtc).HasColumnName("invoiced_at_utc");
            entity.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ux_work_acts_number");
            entity.HasIndex(x => new { x.ProjectId, x.Status }).HasDatabaseName("ix_work_acts_project_id_status");
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureWorkActLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<WorkActLine>(entity =>
        {
            entity.ToTable("work_act_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.WorkActId).HasColumnName("work_act_id");
            entity.Property(x => x.ExternalId).HasColumnName("external_id").HasMaxLength(FieldLengths.WorkActLine.ExternalId);
            entity.Property(x => x.Source).HasColumnName("source").HasConversion<int>();
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(FieldLengths.WorkActLine.Description).IsRequired();
            entity.Property(x => x.WorkDate).HasColumnName("work_date").HasColumnType("date");
            entity.Property(x => x.DurationInMinutes).HasColumnName("duration_in_minutes");
            entity.Property(x => x.Notes).HasColumnName("notes");
            entity.Property(x => x.HourlyRate).HasColumnName("hourly_rate");
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
            entity.HasIndex(x => new { x.WorkActId, x.WorkDate }).HasDatabaseName("ix_work_act_lines_work_act_id_work_date");
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureTimeTracking(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TimeTracking>(entity =>
        {
            entity.ToTable("time_trackings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.StartedAtLocal).HasColumnName("started_at_local");
            entity.Property(x => x.DurationInMinutes).HasColumnName("duration_in_minutes");
            entity.Property(x => x.HourlyRate).HasColumnName("hourly_rate").IsRequired(false);
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(FieldLengths.TimeTracking.Description).IsRequired();
            entity.HasIndex(x => new { x.ProjectId, x.StartedAtLocal }).HasDatabaseName("ix_time_trackings_project_id_started_at_local");
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureUnit(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Unit>(entity =>
        {
            entity.ToTable("units");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Code).HasColumnName("code").HasMaxLength(FieldLengths.Unit.Code).IsRequired();
            entity.Property(x => x.Name).HasColumnName("name").HasMaxLength(FieldLengths.Unit.Name).IsRequired();
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            entity.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_units_code");
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureInvoice(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.Number).HasColumnName("number").HasMaxLength(FieldLengths.Invoice.Number).IsRequired();
            entity.Property(x => x.SequenceNumber).HasColumnName("sequence_number");
            entity.Property(x => x.IssuerId).HasColumnName("issuer_id");
            entity.Property(x => x.ClientId).HasColumnName("client_id");
            entity.Property(x => x.ProjectId).HasColumnName("project_id");
            entity.Property(x => x.CurrencyCode).HasColumnName("currency_code").HasMaxLength(FieldLengths.Invoice.CurrencyCode).IsRequired();
            entity.Property(x => x.LanguageCode).HasColumnName("language_code").HasMaxLength(FieldLengths.Invoice.LanguageCode).IsRequired();
            entity.Property(x => x.InvoiceTemplateId).HasColumnName("invoice_template_id");
            entity.Property(x => x.IssueDate).HasColumnName("issue_date").HasColumnType("date");
            entity.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
            entity.Property(x => x.SubtotalAmount).HasColumnName("subtotal_amount").HasPrecision(18, 2);
            entity.Property(x => x.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
            entity.Property(x => x.TotalAmountInText).HasColumnName("total_amount_in_text").HasMaxLength(1000).IsRequired();
            entity.Property(x => x.Status).HasColumnName("status").HasConversion<int>();
            entity.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(1000);
            entity.HasIndex(x => x.Number).IsUnique().HasDatabaseName("ux_invoices_number");
            entity.HasIndex(x => x.SequenceNumber).IsUnique().HasDatabaseName("ux_invoices_sequence_number");
            entity.HasIndex(x => new { x.IssuerId, x.IssueDate }).HasDatabaseName("ix_invoices_issuer_id_issue_date");
            entity.HasIndex(x => new { x.ClientId, x.IssueDate }).HasDatabaseName("ix_invoices_client_id_issue_date");

            entity.OwnsOne(x => x.IssuerInfo, owned => ConfigureCompanyInfo(owned, "issuer_info"));
            entity.OwnsOne(x => x.ClientInfo, owned => ConfigureCompanyInfo(owned, "client_info"));
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureInvoiceLine(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceLine>(entity =>
        {
            entity.ToTable("invoice_lines");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.InvoiceId).HasColumnName("invoice_id");
            entity.Property(x => x.LineNo).HasColumnName("line_no");
            entity.Property(x => x.SourceType).HasColumnName("source_type").HasConversion<int>();
            entity.Property(x => x.WorkActId).HasColumnName("work_act_id");
            entity.Property(x => x.Description).HasColumnName("description").HasMaxLength(FieldLengths.InvoiceLine.Description).IsRequired();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasPrecision(18, 2);
            entity.Property(x => x.UnitId).HasColumnName("unit_id");
            entity.Property(x => x.UnitPrice).HasColumnName("unit_price").HasPrecision(18, 4);
            entity.Property(x => x.Amount).HasColumnName("amount").HasPrecision(18, 2);
            entity.Property(x => x.VatApplicable).HasColumnName("vat_applicable");
            entity.Property(x => x.VatRatePercent).HasColumnName("vat_rate_percent").HasPrecision(5, 2);
            entity.Property(x => x.VatAmount).HasColumnName("vat_amount").HasPrecision(18, 2);
            entity.Property(x => x.TotalAmount).HasColumnName("total_amount").HasPrecision(18, 2);
            entity.HasIndex(x => new { x.InvoiceId, x.LineNo }).IsUnique().HasDatabaseName("ux_invoice_lines_invoice_id_line_no");
            entity.HasIndex(x => x.WorkActId).IsUnique().HasDatabaseName("ux_invoice_lines_work_act_id");
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureSystemSettings(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemSettings>(entity =>
        {
            entity.ToTable("system_settings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.DefaultVatRatePercent).HasColumnName("default_vat_rate_percent").HasPrecision(5, 2);
            entity.Property(x => x.InvoiceNumberTemplate).HasColumnName("invoice_number_template").HasMaxLength(FieldLengths.SystemSettings.InvoiceNumberTemplate).IsRequired();
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureInvoiceSequenceState(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<InvoiceSequenceState>(entity =>
        {
            entity.ToTable("invoice_sequence_states");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id).HasColumnName("id");
            entity.Property(x => x.CurrentValue).HasColumnName("current_value");
            entity.Property(x => x.PrefixContext).HasColumnName("prefix_context").HasMaxLength(FieldLengths.InvoiceSequenceState.PrefixContext);
            entity.Property(x => x.ResetRule).HasColumnName("reset_rule").HasConversion<int>();
            ConfigureBaseEntityColumns(entity);
        });
    }

    private static void ConfigureBaseEntityColumns<TEntity>(EntityTypeBuilder<TEntity> entity)
        where TEntity : BaseEntity
    {
        entity.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc");
        entity.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        entity.Property(x => x.UpdatedAtUtc).HasColumnName("updated_at_utc");
        entity.Property(x => x.UpdatedByUserId).HasColumnName("updated_by_user_id");
    }

    private static void ConfigureCompanyInfo<TOwner>(OwnedNavigationBuilder<TOwner, CompanyInfo> owned, string prefix)
        where TOwner : class
    {
        owned.Property(x => x.Name).HasColumnName($"{prefix}_name").HasMaxLength(FieldLengths.CompanyInfo.Name).IsRequired();
        owned.Property(x => x.CompanyCode).HasColumnName($"{prefix}_company_code").HasMaxLength(FieldLengths.CompanyInfo.CompanyCode);
        owned.Property(x => x.VatCode).HasColumnName($"{prefix}_vat_code").HasMaxLength(FieldLengths.CompanyInfo.VatCode);
        owned.Property(x => x.AddressLine1).HasColumnName($"{prefix}_address_line1").HasMaxLength(FieldLengths.CompanyInfo.AddressLine1);
        owned.Property(x => x.AddressLine2).HasColumnName($"{prefix}_address_line2").HasMaxLength(FieldLengths.CompanyInfo.AddressLine2);
        owned.Property(x => x.City).HasColumnName($"{prefix}_city").HasMaxLength(FieldLengths.CompanyInfo.City);
        owned.Property(x => x.PostalCode).HasColumnName($"{prefix}_postal_code").HasMaxLength(FieldLengths.CompanyInfo.PostalCode);
        owned.Property(x => x.CountryCode).HasColumnName($"{prefix}_country_code").HasMaxLength(FieldLengths.CompanyInfo.CountryCode);
        owned.Property(x => x.ContactName).HasColumnName($"{prefix}_contact_name").HasMaxLength(FieldLengths.CompanyInfo.ContactName);
        owned.Property(x => x.ContactEmail).HasColumnName($"{prefix}_contact_email").HasMaxLength(FieldLengths.CompanyInfo.ContactEmail);
        owned.Property(x => x.ContactPhone).HasColumnName($"{prefix}_contact_phone").HasMaxLength(FieldLengths.CompanyInfo.ContactPhone);
        owned.Property(x => x.BankName).HasColumnName($"{prefix}_bank_name").HasMaxLength(FieldLengths.CompanyInfo.BankName);
        owned.Property(x => x.BankAccount).HasColumnName($"{prefix}_bank_account").HasMaxLength(FieldLengths.CompanyInfo.BankAccount);
        owned.Property(x => x.BankSwift).HasColumnName($"{prefix}_bank_swift").HasMaxLength(FieldLengths.CompanyInfo.BankSwift);
        owned.Property(x => x.VatPayerCode).HasColumnName($"{prefix}_vat_payer_code").HasMaxLength(FieldLengths.CompanyInfo.VatPayerCode);
    }
}
