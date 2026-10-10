using Apskaita.Web.Features.Admin.IssuerRegistry.Models;
using Apskaita.Web.Features.Admin.IssuerRegistry.Services;
using Apskaita.Web.Features.Admin.IssuerRegistry.Slices;
using Apskaita.Web.Features.Admin.Common.Models;
using Apskaita.Web.Features.Admin.Common.Slices;
using Apskaita.Web.Shared;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.IssuerRegistry.Endpoints;

public sealed class IssuersEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<IssuerRegistryWorkspaceFactory>(resultBuilder)
{
    private readonly IssuersContentService _content = new();
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "issuers", "Issuers", "list", Order: 20)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] IssuerSearchModel search,
            IssuerListQuery query,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var model = await _content.CreateListModelAsync(db, search, query, ct);
            if (ctx.IsHtmxTarget("issuers-list"))
            {
                return Result.Fragment(_IssuersList.Create(model))
                    .WithState(search)
                    .WithState(query);
            }

            return Result.Page(_ListPage.Create(model))
                .WithState(search)
                .WithState(query);
        });

        group.MapGet("create", () =>
            Result.Page(_CreatePage.Create(_content.CreateCreateFormModel())));

        group.MapPost("create", async (
            IssuerUpsertRequest request,
            IValidator<IssuerUpsertRequest> validator,
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

            return Result.Page(_CreatePage.Create(_content.CreateCreateFormModel()))
                .WithTrigger("Issuers.ListRefresh")
                .WithToast($"Issuer '{request.Name}' was created.");
        });

        group.MapGet("edit/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row)))
                ;
        });

        group.MapPost("edit/{id:guid}", async (
            Guid id,
            IssuerUpsertRequest request,
            IValidator<IssuerUpsertRequest> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            _content.TrimRequest(request);
            var validationResult = await validator.ValidateAsync(request, ct);

            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                request.IsActive = row.IsActive;
                return Result.Page(_EditPage.Create(request));
            }

            await _content.ApplyAsync(db, row, request, ct);

            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row)))
                .WithTrigger("Issuers.ListRefresh")
                .WithToast($"Issuer '{request.Name}' was updated.");
        });

        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();

            row.IsActive = !row.IsActive;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            var model = new StatusToggleModel(id, row.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#issuer-status-badge", _StatusBadge.Create(model))
                .WithToast($"Issuer '{row.Info.Name}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        group.MapGet("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            return Result.ShowDialog(_IssuerDelete.Create(_content.CreateDeleteDialogModel(row)));
        });

        group.MapPost("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            await _content.DeleteAsync(db, row, ct);

            return Result.Page(_Empty.Create())
                .WithTrigger("Issuers.ListRefresh")
                .WithToast($"Issuer '{row.Info.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}

