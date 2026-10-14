using Apskaita.Entities.Contracts;
using Apskaita.Feature.Admin.UnitRegistry.Models;
using Apskaita.Feature.Admin.UnitRegistry.Slices;
using Apskaita.Shared.Ui.Common.Models;
using Apskaita.Shared.Ui.Common.Slices;
using Apskaita.Feature.Admin.Persistence;
using Apskaita.Web.Shared;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Admin.UnitRegistry.Endpoints;

public sealed class UnitsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<UnitRegistryWorkspaceFactory>(resultBuilder)
{
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "units", "Units", "list", Order: 30, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        // GET list — read seam (IQuery<Unit>).
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] UnitSearchModel search,
            UnitListQuery query,
            IQuery<Unit> units,
            CancellationToken ct) =>
        {
            var items = units.All().Select(x => new UnitRowModel
            {
                Id = x.Id,
                Code = x.Code,
                IsActive = x.IsActive,
                Name = x.Name
            });

            if (!string.IsNullOrWhiteSpace(search.Search))
            {
                var term = search.Search.Trim();
                items = items.Where(x => x.Name.Contains(term) || x.Code.Contains(term));
            }

            var paged = await items.ToPagedListAsync(query, ct);
            var model = new UnitListModel(paged.Total, search, query, paged.Items);

            if (ctx.IsHtmxTarget("units-list"))
            {
                return Result.Fragment(_UnitsList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model)).WithState(search).WithState(query);
        });

        group.MapGet("create", () =>
            Result.Page(_CreatePage.Create(UnitUpsertRequest.CreateDefault())));

        // POST create — write seam (IAdminStore).
        group.MapPost("create", async (
            UnitUpsertRequest request,
            IValidator<UnitUpsertRequest> validator,
            IAdminStore store,
            CancellationToken ct) =>
        {
            request.Normalize();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                return Result.Page(_CreatePage.Create(request));
            }

            var now = DateTime.UtcNow;
            store.Units.Add(new Unit
            {
                Id = Guid.NewGuid(),
                Code = request.Code ?? string.Empty,
                Name = request.Name ?? string.Empty,
                IsActive = true,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                CreatedByUserId = Guid.Empty,
                UpdatedByUserId = Guid.Empty
            });
            await store.SaveChangesAsync(ct);

            return Result.Page(_CreatePage.Create(UnitUpsertRequest.CreateDefault()))
                .WithTrigger("Units.ListRefresh")
                .WithToast($"Unit '{request.Name}' was created.");
        });

        // GET edit — read seam (no-tracking find for display).
        group.MapGet("edit/{id:guid}", async (Guid id, IQuery<Unit> units, CancellationToken ct) =>
        {
            var row = await units.All().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();
            return Result.Page(_EditPage.Create(UnitUpsertRequest.FromEntity(row)));
        });

        // POST edit — write seam (tracked load for mutation).
        group.MapPost("edit/{id:guid}", async (
            Guid id,
            UnitUpsertRequest request,
            IValidator<UnitUpsertRequest> validator,
            IAdminStore store,
            CancellationToken ct) =>
        {
            var row = await store.Units.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();

            request.Normalize();
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                request.Errors = validationResult.ToErrorDictionary();
                request.IsActive = row.IsActive;
                return Result.Page(_EditPage.Create(request));
            }

            row.Code = request.Code ?? string.Empty;
            row.Name = request.Name ?? string.Empty;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;
            await store.SaveChangesAsync(ct);

            return Result.Page(_EditPage.Create(UnitUpsertRequest.FromEntity(row)))
                .WithTrigger("Units.ListRefresh")
                .WithToast($"Unit '{request.Name}' was updated.");
        });

        // POST toggle — write seam (tracked load + mutate).
        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.Units.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();

            row.IsActive = !row.IsActive;
            row.UpdatedAtUtc = DateTime.UtcNow;
            await store.SaveChangesAsync(ct);

            var model = new StatusToggleModel(id, row.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#unit-status-badge", _StatusBadge.Create(model))
                .WithToast($"Unit '{row.Name}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        // GET delete — read seam (no-tracking find for the dialog).
        group.MapGet("delete/{id:guid}", async (Guid id, IQuery<Unit> units, CancellationToken ct) =>
        {
            var row = await units.All().FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_UnitDelete.Create(new UnitDeleteDialogModel(row.Id, row.Name)));
        });

        // POST delete — write seam (tracked load + remove).
        group.MapPost("delete/{id:guid}", async (Guid id, IAdminStore store, CancellationToken ct) =>
        {
            var row = await store.Units.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (row is null) return Results.NotFound();

            store.Units.Remove(row);
            await store.SaveChangesAsync(ct);

            return Result.Page(_Empty.Create())
                .WithTrigger("Units.ListRefresh")
                .WithToast($"Unit '{row.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}
