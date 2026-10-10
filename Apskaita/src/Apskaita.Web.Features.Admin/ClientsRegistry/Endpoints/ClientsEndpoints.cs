using Apskaita.Web.Features.Admin.ClientsRegistry.Models;
using Apskaita.Web.Features.Admin.ClientsRegistry.Services;
using Apskaita.Web.Features.Admin.ClientsRegistry.Slices;
using Apskaita.Web.Features.Admin.Common.Models;
using Apskaita.Web.Features.Admin.Common.Slices;
using Apskaita.Web.Shared;
using FluentValidation;

namespace Apskaita.Web.Features.Admin.ClientsRegistry.Endpoints;

public sealed class ClientsEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<ClientsWorkspaceFactory>(resultBuilder)
{
    private readonly ClientsContentService _content = new();
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "clients", "Clients", "list", Order: 10)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] ClientSearchModel search,
            ClientListQuery query,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var model = await _content.CreateListModelAsync(db, search, query, ct);
            if (ctx.IsHtmxTarget("clients-list"))
            {
                return Result.Fragment(_ClientsList.Create(model))
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
            ClientUpsertRequest request,
            IValidator<ClientUpsertRequest> validator,
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
                .WithTrigger("clients-list-refresh")
                .WithToast($"Client '{request.Name}' was created.");
        });

        group.MapGet("edit/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            return Result.Page(_EditPage.Create(_content.CreateEditFormModel(row)));
        });

        group.MapPost("edit/{id:guid}", async (
            Guid id,
            ClientUpsertRequest request,
            IValidator<ClientUpsertRequest> validator,
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
                .WithTrigger("clients-list-refresh")
                .WithToast($"Client '{request.Name}' was updated.");
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
                .WithOob("#client-status-badge", _StatusBadge.Create(model))
                .WithToast($"Client '{row.Info.Name}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        group.MapGet("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            return Result.ShowDialog(_ClientDelete.Create(_content.CreateDeleteDialogModel(row)));
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
                .WithTrigger("clients-list-refresh")
                .WithToast($"Client '{row.Info.Name}' was deleted.", title: "Deleted", tone: ToastTone.Warning)
                .ClearDialog();
        });
    }
}
