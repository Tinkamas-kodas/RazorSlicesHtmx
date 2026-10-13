using Apskaita.Web.Features.Admin.ProjectRegistry.Models;
using Apskaita.Web.Shared.ProjectColors;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.ProjectRegistry.Services;

/// <summary>
/// Projects-specific shared helpers: form-model factories, request trimming, and option-list population.
/// Only methods reused across endpoints live here; single-use mapping is inlined in
/// <c>ProjectsEndpoints</c>. No <c>DbContext</c> and no store — reads go through <c>IQuery&lt;T&gt;</c>
/// (the read seam), writes are done by the endpoints through <c>IAdminStore</c>. Replaces the former
/// <c>ProjectsContentService</c>.
/// </summary>
internal static class ProjectForms
{
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
