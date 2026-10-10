using Apskaita.Web.Features.Admin.UserRegistry.Models;
using Apskaita.Web.Features.Admin.UserRegistry.Services;
using Apskaita.Web.Features.Admin.UserRegistry.Slices;
using Apskaita.Web.Features.Admin.Common.Models;
using Apskaita.Web.Features.Admin.Common.Slices;
using Apskaita.Web.Shared;

namespace Apskaita.Web.Features.Admin.UserRegistry.Endpoints;

public sealed class UsersEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<UserRegistryWorkspaceFactory>(resultBuilder)
{
    private readonly UsersContentService _content = new();
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "users", "Users", "list", Order: 70)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] UserSearchModel search,
            UserListQuery query,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var model = await _content.CreateListModelAsync(db, search, query, ct);
            if (ctx.IsHtmxTarget("users-list"))
            {
                return Result.Fragment(_UsersList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model))
                .WithState(search)
                .WithState(query);
        });

        group.MapGet("view/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindViewByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();
            return Result.Page(_ViewPage.Create(row));
        });

        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await _content.FindByIdAsync(db, id, ct);
            if (row is null) return Results.NotFound();

            await _content.ToggleActiveAsync(db, row, ct);

            var model = new StatusToggleModel(id, row.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#user-status-badge", _StatusBadge.Create(model))
                .WithToast($"User '{row.DisplayName}' was {(row.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: row.IsActive ? ToastTone.Success : ToastTone.Warning);
        });
    }
}

