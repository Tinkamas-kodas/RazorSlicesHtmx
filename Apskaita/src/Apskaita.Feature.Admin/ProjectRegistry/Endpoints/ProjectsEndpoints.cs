using Apskaita.Web.Features.Admin.ProjectRegistry.Models;
using Apskaita.Web.Features.Admin.ProjectRegistry.Services;
using Apskaita.Web.Features.Admin.ProjectRegistry.Slices;
using Apskaita.Web.Features.Admin.Common.Models;
using Apskaita.Web.Features.Admin.Common.Slices;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.ProjectColors;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Web.Features.Admin.ProjectRegistry.Endpoints;

public sealed class ProjectsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<ProjectRegistryWorkspaceFactory>(resultBuilder)
{
    private const string SampleTime = "10:00 - 11:30";
    private const string SampleDescription = "Prepare monthly report\nReview invoice details";

    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "projects", "Projects", "list", Order: 40, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] ProjectSearchModel search,
            ProjectListQuery query,
            IQuery<Project> projects,
            CancellationToken ct) =>
        {
            var items = projects.All().Select(p => new ProjectRowModel
            {
                Id = p.Id,
                Name = p.Name,
                Code = p.Code,
                ClientName = p.Client.Info.Name,
                HourlyRate = p.HourlyRate,
                CurrencyCode = p.CurrencyCode,
                TemplateName = p.InvoiceTemplate.Name,
                IsActive = p.IsActive
            });

            if (!string.IsNullOrWhiteSpace(search.Search))
            {
                var term = search.Search.Trim();
                items = items.Where(x => x.Name.Contains(term) || (x.Code != null && x.Code.Contains(term)) || x.CurrencyCode.Contains(term));
            }

            var paged = await items.ToPagedListAsync(query, ct);
            var model = new ProjectListModel(paged.Total, search, query, paged.Items);

            if (ctx.IsHtmxTarget("projects-list"))
            {
                return Result.Fragment(_ProjectsList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model)).WithState(search).WithState(query);
        });

        // GET create — read seam for the client/template option lists.
        group.MapGet("create", async (IQuery<Client> clients, IQuery<InvoiceTemplate> templates, CancellationToken ct) =>
        {
            var model = ProjectForms.CreateCreateFormModel();
            await ProjectForms.PopulateOptionsAsync(model, clients, templates, ct);
            return Result.Page(_CreatePage.Create(model));
        });

        group.MapGet("color-lookup", (string? query) =>
        {
            var items = ProjectColorCatalog.Search(query);
            return Result.Fragment(_ColorLookupItems.Create(items));
        });

        group.MapGet("color-lookup-selected", (string value) =>
        {
            var normalized = ProjectColorCatalog.Normalize(value);
            return Results.Ok(new
            {
                value = normalized,
                text = ProjectColorCatalog.GetLookupText(normalized)
            });
        });

        group.MapGet("color-preview", (string? colorRgb, string? colorLabel) =>
        {
            var presentation = ProjectColorCatalog.CreatePresentation(colorRgb);
            var normalized = ProjectColorCatalog.NormalizeOrDefault(colorRgb);
            var label = string.IsNullOrWhiteSpace(colorLabel)
                ? ProjectColorCatalog.GetLookupText(normalized)
                : colorLabel.Trim();
            var model = new ProjectColorPreviewModel(
                presentation.BackgroundColorRgb,
                presentation.AccentColorRgb,
                presentation.TextColorRgb,
                label,
                normalized,
                SampleTime,
                SampleDescription);
            return Result.Fragment(_ColorPreview.Create(model));
        });

        // POST create — write seam (IAdminStore); form rebuild reads options via IQuery.
        group.MapPost("create", async (
            ProjectUpsertRequest request,
            IValidator<ProjectUpsertRequest> validator,
            IAdminStore store,
            IQuery<Client> clients,
            IQuery<InvoiceTemplate> templates,
            CancellationToken ct) =>
        {
            ProjectForms.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                await ProjectForms.PopulateOptionsAsync(request, clients, templates, ct);
                return Result.Page(_CreatePage.Create(request));
            }

            var now = DateTime.UtcNow;
            store.Projects.Add(new Project
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
            });
            await store.SaveChangesAsync(ct);

            var model = ProjectForms.CreateCreateFormModel();
            await ProjectForms.PopulateOptionsAsync(model, clients, templates, ct);
            return Result.Page(_CreatePage.Create(model)).WithTrigger("Projects.ListRefresh").WithToast($"Project '{request.Name}' was created.");
        });

        // GET edit — read seam (no-tracking find for display).
        group.MapGet("edit/{id:guid}", async (
            Guid id,
            IQuery<Project> projects,
            IQuery<Client> clients,
            IQuery<InvoiceTemplate> templates,
            CancellationToken ct) =>
        {
            var row = await projects.All().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (row is null) return Results.NotFound();

            var model = ProjectForms.CreateEditFormModel(row);
            await ProjectForms.PopulateOptionsAsync(model, clients, templates, ct);
            return Result.Page(_EditPage.Create(model));
        });

        // POST edit — write seam (tracked load for mutation); form rebuild reads options via IQuery.
        group.MapPost("edit/{id:guid}", async (
            Guid id,
            ProjectUpsertRequest request,
            IValidator<ProjectUpsertRequest> validator,
            IAdminStore store,
            IQuery<Client> clients,
            IQuery<InvoiceTemplate> templates,
            CancellationToken ct) =>
        {
            var row = await store.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (row is null) return Results.NotFound();

            ProjectForms.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                request.IsActive = row.IsActive;
                await ProjectForms.PopulateOptionsAsync(request, clients, templates, ct);
                return Result.Page(_EditPage.Create(request));
            }

            row.ClientId = request.ClientId ?? Guid.Empty;
            row.Name = request.Name ?? string.Empty;
            row.Code = request.Code;
            row.HourlyRate = request.HourlyRate;
            row.ColorRgb = ProjectColorCatalog.NormalizeOrDefault(request.ColorRgb);
            row.CurrencyCode = request.CurrencyCode ?? "EUR";
            row.InvoiceTemplateId = request.InvoiceTemplateId ?? Guid.Empty;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;
            await store.SaveChangesAsync(ct);

            var model = ProjectForms.CreateEditFormModel(row);
            await ProjectForms.PopulateOptionsAsync(model, clients, templates, ct);
            return Result.Page(_EditPage.Create(model)).WithTrigger("Projects.ListRefresh").WithToast($"Project '{request.Name}' was updated.");
        });

        // POST toggle — write seam (tracked load + mutate).
        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (row is null) return Results.NotFound();

            row.IsActive = !row.IsActive;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await store.SaveChangesAsync(ct);

            var model = new StatusToggleModel(id, row.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#project-status-badge", _StatusBadge.Create(model))
                .WithToast($"Project '{row.Name}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        // GET delete — read seam (no-tracking find for the dialog).
        group.MapGet("delete/{id:guid}", async (Guid id, IQuery<Project> projects, CancellationToken ct) =>
        {
            var row = await projects.All().FirstOrDefaultAsync(p => p.Id == id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_ProjectDelete.Create(new ProjectDeleteDialogModel(row.Id, row.Name)));
        });

        // POST delete — write seam (tracked load + remove).
        group.MapPost("delete/{id:guid}", async (Guid id, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.Projects.FirstOrDefaultAsync(p => p.Id == id, ct);
            if (row is null) return Results.NotFound();

            store.Projects.Remove(row);
            await store.SaveChangesAsync(ct);

            return Result.Page(_Empty.Create())
                .WithTrigger("Projects.ListRefresh")
                .WithToast($"Project '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}
