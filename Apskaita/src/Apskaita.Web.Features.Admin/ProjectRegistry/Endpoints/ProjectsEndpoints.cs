using Apskaita.Web.Features.Admin.ProjectRegistry.Models;
using Apskaita.Web.Features.Admin.ProjectRegistry.Services;
using Apskaita.Web.Features.Admin.ProjectRegistry.Slices;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.ProjectColors;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.ProjectRegistry.Endpoints;

public sealed class ProjectsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<ProjectRegistryWorkspaceFactory>(resultBuilder)
{
    private readonly ProjectsContentService _content = new();
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "projects", "Projects", "list", Order: 40)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] ProjectSearchModel search,
            ProjectListQuery query,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var model = await _content.CreateListModelAsync(db, search, query, ct);
            if (ctx.IsHtmxTarget("projects-list"))
            {
                return Result.Fragment(_ProjectsList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model)).WithState(search).WithState(query);
        });

        group.MapGet("create", async (ApskaitaDbContext db, CancellationToken ct) =>
            Result.Page(_CreatePage.Create(await _content.CreateCreateFormModelAsync(db, ct))));

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
            var model = _content.CreateColorPreviewModel(colorRgb, colorLabel);
            return Result.Fragment(_ColorPreview.Create(model));
        });

        group.MapPost("create", async (
            ProjectUpsertRequest request,
            IValidator<ProjectUpsertRequest> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            _content.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                await _content.PopulateOptionsAsync(db, request, ct);
                return Result.Page(_CreatePage.Create(request));
            }

            await _content.CreateAsync(db, request, ct);
            return Result.Page(_CreatePage.Create(await _content.CreateCreateFormModelAsync(db, ct))).WithTrigger("Projects.ListRefresh").WithToast($"Project '{request.Name}' was created.");
        });

        group.MapGet("edit/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            var model = await _content.CreateEditFormModelAsync(db, row, ct);
            return Result.Page(_EditPage.Create(model));
        });

        group.MapPost("edit/{id:guid}", async (
            Guid id,
            ProjectUpsertRequest request,
            IValidator<ProjectUpsertRequest> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();

            _content.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                await _content.PopulateOptionsAsync(db, request, ct);
                return Result.Page(_EditPage.Create(request));
            }

            await _content.ApplyAsync(db, row, request, ct);
            return Result.Page(_EditPage.Create(await _content.CreateEditFormModelAsync(db, row, ct))).WithTrigger("Projects.ListRefresh").WithToast($"Project '{request.Name}' was updated.");
        });

        group.MapGet("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_ProjectDelete.Create(_content.CreateDeleteDialogModel(row)));
        });

        group.MapPost("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            await _content.DeleteAsync(db, row, ct);

            return Result.Page(_Empty.Create())
                .WithTrigger("Projects.ListRefresh")
                .WithToast($"Project '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}

