using Apskaita.Web.Features.Admin.ProjectRegistry.Models;
using Apskaita.Web.Shared.ProjectColors;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.ProjectRegistry.Services;

public sealed class ProjectsContentService
{
    private const string SampleTime = "10:00 - 11:30";
    private const string SampleDescription = "Prepare monthly report\nReview invoice details";
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();

    public async Task<ProjectListModel> CreateListModelAsync(ApskaitaDbContext db, ProjectSearchModel search, ProjectListQuery query, CancellationToken ct)
    {
        var items =
            from p in db.Projects.AsNoTracking()
            join c in db.Clients.AsNoTracking() on p.ClientId equals c.Id
            join t in db.InvoiceTemplates.AsNoTracking() on p.InvoiceTemplateId equals t.Id
            select new ProjectRowModel
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                ClientName = c.Info.Name,
                HourlyRate = p.HourlyRate,
                CurrencyCode = p.CurrencyCode,
                TemplateName = t.Name,
                IsActive = p.IsActive
            };

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            items = items.Where(x => x.Name.Contains(term) || (x.Code != null && x.Code.Contains(term)) || x.CurrencyCode.Contains(term));
        }

        var paged = await items.ToPagedListAsync(query, ct);
        return new ProjectListModel(paged.Total, search, query, paged.Items);
    }

    public async Task<ProjectUpsertRequest> CreateCreateFormModelAsync(ApskaitaDbContext db, CancellationToken ct)
    {
        var model = new ProjectUpsertRequest
        {
            ColorRgb = ProjectColorCatalog.DefaultColorRgb,
            ColorLabel = ProjectColorCatalog.GetLookupText(ProjectColorCatalog.DefaultColorRgb),
            CurrencyCode = "EUR",
            IsActive = true,
            Errors = EmptyErrors
        };

        await PopulateOptionsAsync(db, model, ct);
        return model;
    }

    public async Task<ProjectUpsertRequest> CreateEditFormModelAsync(ApskaitaDbContext db, Project row, CancellationToken ct)
    {
        var model = new ProjectUpsertRequest
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

        await PopulateOptionsAsync(db, model, ct);
        return model;
    }

    public async Task PopulateOptionsAsync(ApskaitaDbContext db, ProjectUpsertRequest model, CancellationToken ct)
    {
        model.Clients = await db.Clients.AsNoTracking().OrderBy(x => x.Info.Name)
            .Select(x => new ProjectOptionModel(x.Id, x.Info.Name))
            .ToListAsync(ct);

        model.Templates = await db.InvoiceTemplates.AsNoTracking().OrderBy(x => x.Name)
            .Select(x => new ProjectOptionModel(x.Id, x.Name))
            .ToListAsync(ct);
    }

    public ProjectDeleteDialogModel CreateDeleteDialogModel(Project row) => new(row.Id, row.Name);

    public ProjectColorPreviewModel CreateColorPreviewModel(string? colorRgb, string? colorLabel)
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

    public void TrimRequest(ProjectUpsertRequest request)
    {
        request.Name = request.Name?.Trim();
        request.Code = request.Code?.Trim();
        request.ColorRgb = ProjectColorCatalog.Normalize(request.ColorRgb);
        request.ColorLabel = string.IsNullOrWhiteSpace(request.ColorLabel)
            ? ProjectColorCatalog.GetLookupText(request.ColorRgb)
            : request.ColorLabel.Trim();
        request.CurrencyCode = request.CurrencyCode?.Trim().ToUpperInvariant();
    }

    public Task<Project?> FindByIdAsync(ApskaitaDbContext db, Guid id, CancellationToken ct) => db.Projects.FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task CreateAsync(ApskaitaDbContext db, ProjectUpsertRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var row = new Project
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

        db.Projects.Add(row);
        await db.SaveChangesAsync(ct);
    }

    public async Task ApplyAsync(ApskaitaDbContext db, Project row, ProjectUpsertRequest request, CancellationToken ct)
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

        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(ApskaitaDbContext db, Project row, CancellationToken ct)
    {
        db.Projects.Remove(row);
        await db.SaveChangesAsync(ct);
    }
}

