using Apskaita.Web.Features.Admin.UnitRegistry.Models;
using Apskaita.Web.Features.Admin.UnitRegistry.Services;
using Apskaita.Web.Features.Admin.UnitRegistry.Slices;
using Apskaita.Web.Shared;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.UnitRegistry.Endpoints;

public sealed class UnitsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<UnitRegistryWorkspaceFactory>(resultBuilder)
{
    private readonly UnitsContentService _content = new();
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "units", "Units", "list", Order: 30)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] UnitSearchModel search,
            UnitListQuery query,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var model = await _content.CreateListModelAsync(db, search, query, ct);
            if (ctx.IsHtmxTarget("units-list"))
            {
                return Result.Fragment(_UnitsList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model))
                .WithState(search)
                .WithState(query);
        });

        group.MapGet("create", () =>
            Result.Page(_CreatePage.Create(_content.CreateCreateFormModel())));

        group.MapPost("create", async (
            UnitUpsertRequest request,
            IValidator<UnitUpsertRequest> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            _content.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                return Result.Page(_CreatePage.Create(request));
            }

            await _content.CreateAsync(db, request, ct);
            return Result.Page(_CreatePage.Create(_content.CreateCreateFormModel())).WithTrigger("Units.ListRefresh").WithToast($"Unit '{request.Name}' was created.");
        });

        group.MapGet("edit/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row)));
        });

        group.MapPost("edit/{id:guid}", async (
            Guid id,
            UnitUpsertRequest request,
            IValidator<UnitUpsertRequest> validator,
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
                return Result.Page(_EditPage.Create(request));
            }

            await _content.ApplyAsync(db, row, request, ct);
            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row))).WithTrigger("Units.ListRefresh").WithToast($"Unit '{request.Name}' was updated.");
        });

        group.MapGet("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_UnitDelete.Create(_content.CreateDeleteDialogModel(row)));
        });

        group.MapPost("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();

            await _content.DeleteAsync(db, row, ct);
            return Result.Page(_Empty.Create())
                .WithTrigger("Units.ListRefresh")
                .WithToast($"Unit '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}

