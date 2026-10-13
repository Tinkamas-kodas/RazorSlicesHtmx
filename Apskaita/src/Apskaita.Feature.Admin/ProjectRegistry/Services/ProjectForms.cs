using Apskaita.Entities;
using Apskaita.Web.Features.Admin.ProjectRegistry.Models;
using Apskaita.Web.Shared.ProjectColors;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.ProjectRegistry.Services;

/// <summary>
/// Projects-specific pure helpers: request/entity mapping, form-model factories, color preview, and
/// option-list population. No <c>DbContext</c> and no store — reads go through <c>IQuery&lt;T&gt;</c>
/// (the read seam), writes are done by the endpoints through <c>IAdminStore</c>. Replaces the former
/// <c>ProjectsContentService</c>.
/// </summary>
internal static class ProjectForms
{
    private const string SampleTime = "10:00 - 11:30";
    private const string SampleDescription = "Prepare monthly report\nReview invoice details";
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public static ProjectUpsertRequest CreateCreateFormModel() => new()
    {
        ColorRgb = ProjectColorCatalog.DefaultColorRgb,
        ColorLabel = ProjectColorCatalog.GetLookupText(ProjectColorCatalog.DefaultColorRgb),
        CurrencyCode = "EUR",
        IsActive = true,
        Errors = EmptyErrors
    };

    public static ProjectUpsertRequest CreateEditFormModel(Project row) => new()
    {
        Id = row.Id,
        ClientId = row.ClientId,
        Name = row.Name,
        Code = row.Code,
        HourlyRate = row.HourlyRate,
        ColorRgb = ProjectColorCatalog.NormalizeOrDefault(row.ColorRgb),
        ColorLabel = ProjectColorCatalog.GetLookupText(row.ColorRgb),
        CurrencyCode = row.CurrencyCode,
        InvoiceTemplateId = row.InvoiceTemplateId,
        IsActive = row.IsActive,
        Errors = EmptyErrors
    };

    public static ProjectDeleteDialogModel CreateDeleteDialogModel(Project row) => new(row.Id, row.Name);

    public static ProjectColorPreviewModel CreateColorPreviewModel(string? colorRgb, string? colorLabel)
    {
        var presentation = ProjectColorCatalog.CreatePresentation(colorRgb);
        var normalized = ProjectColorCatalog.NormalizeOrDefault(colorRgb);
        var label = string.IsNullOrWhiteSpace(colorLabel)
            ? ProjectColorCatalog.GetLookupText(normalized)
            : colorLabel.Trim();

        return new ProjectColorPreviewModel(
            presentation.BackgroundColorRgb,
            presentation.AccentColorRgb,
            presentation.TextColorRgb,
            label,
            normalized,
            SampleTime,
            SampleDescription);
    }

    public static void TrimRequest(ProjectUpsertRequest request)
    {
        request.Name = request.Name?.Trim();
        request.Code = request.Code?.Trim();
        request.ColorRgb = ProjectColorCatalog.Normalize(request.ColorRgb);
        request.ColorLabel = string.IsNullOrWhiteSpace(request.ColorLabel)
            ? ProjectColorCatalog.GetLookupText(request.ColorRgb)
            : request.ColorLabel.Trim();
        request.CurrencyCode = request.CurrencyCode?.Trim().ToUpperInvariant();
    }

    public static Project ToEntity(ProjectUpsertRequest request)
    {
        var now = DateTime.UtcNow;
        return new Project
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId ?? Guid.Empty,
            Name = request.Name ?? string.Empty,
            Code = request.Code,
            HourlyRate = request.HourlyRate,
            ColorRgb = ProjectColorCatalog.NormalizeOrDefault(request.ColorRgb),
            CurrencyCode = request.CurrencyCode ?? "EUR",
            InvoiceTemplateId = request.InvoiceTemplateId ?? Guid.Empty,
            IsActive = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            CreatedByUserId = Guid.Empty,
            UpdatedByUserId = Guid.Empty
        };
    }

    public static void ApplyToEntity(Project row, ProjectUpsertRequest request)
    {
        row.ClientId = request.ClientId ?? Guid.Empty;
        row.Name = request.Name ?? string.Empty;
        row.Code = request.Code;
        row.HourlyRate = request.HourlyRate;
        row.ColorRgb = ProjectColorCatalog.NormalizeOrDefault(request.ColorRgb);
        row.CurrencyCode = request.CurrencyCode ?? "EUR";
        row.InvoiceTemplateId = request.InvoiceTemplateId ?? Guid.Empty;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedByUserId = Guid.Empty;
    }

    public static async Task PopulateOptionsAsync(ProjectUpsertRequest model, IQuery<Client> clients, IQuery<InvoiceTemplate> templates, CancellationToken ct)
    {
        model.Clients = await clients.All().OrderBy(x => x.Info.Name)
            .Select(x => new ProjectOptionModel(x.Id, x.Info.Name))
            .ToListAsync(ct);

        model.Templates = await templates.All().OrderBy(x => x.Name)
            .Select(x => new ProjectOptionModel(x.Id, x.Name))
            .ToListAsync(ct);
    }
}
