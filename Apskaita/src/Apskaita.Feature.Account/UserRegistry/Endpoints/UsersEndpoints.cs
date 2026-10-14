using Apskaita.Entities.Contracts;
using Apskaita.Feature.Account.Persistence;
using Apskaita.Feature.Account.UserRegistry.Models;
using Apskaita.Feature.Account.UserRegistry.Slices;
using Apskaita.Shared.Ui.Common.Models;
using Apskaita.Shared.Ui.Common.Slices;
using Apskaita.Web.Shared;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Account.UserRegistry.Endpoints;

/// <summary>
/// Admin user-management endpoints. Owned by Feature.Account (the <c>users</c> table is Account's):
/// reads compose over <see cref="IQuery{T}"/> (no-tracking), writes mutate through
/// <see cref="IAccountStore"/> (tracked). Registered under the <c>/admin/users</c> route group;
/// navigation placement under the Admin group is an <c>AdminNavItem</c> concern, not an assembly concern.
/// </summary>
public sealed class UsersEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModuleWithWorkspace<UserRegistryWorkspaceFactory>(resultBuilder)
{
    public override string FeatureDivId => "feature-details";

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("ADMIN", "users", "Users", "list", Order: 70, AuthorizationPolicy: AuthPolicies.Admin)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Admin);

        group.MapGet("list", async (
            HttpContext ctx,
            [AsParameters] UserSearchModel search,
            UserListQuery query,
            IQuery<User> users,
            CancellationToken ct) =>
        {
            var model = await CreateListModelAsync(users, search, query, ct);
            if (ctx.IsHtmxTarget("users-list"))
            {
                return Result.Fragment(_UsersList.Create(model)).WithState(search).WithState(query);
            }

            return Result.Page(_ListPage.Create(model))
                .WithState(search)
                .WithState(query);
        });

        group.MapGet("view/{id:guid}", async (Guid id, IQuery<User> users, CancellationToken ct) =>
        {
            var row = await FindViewByIdAsync(users, id, ct);
            if (row is null) return Results.NotFound();
            return Result.Page(_ViewPage.Create(row));
        });

        group.MapPost("toggle/{id:guid}", async (Guid id, HttpContext ctx, IAccountStore store, CancellationToken ct) =>
        {
            var user = await store.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (user is null) return Results.NotFound();

            user.IsActive = !user.IsActive;
            await store.SaveChangesAsync(ct);

            var model = new StatusToggleModel(id, user.IsActive, ctx.Request.Path.Value ?? string.Empty);
            return Result.Fragment(_StatusAction.Create(model))
                .WithOob("#user-status-badge", _StatusBadge.Create(model))
                .WithToast($"User '{user.DisplayName}' was {(user.IsActive ? "activated" : "deactivated")}.",
                    title: "Status", tone: user.IsActive ? ToastTone.Success : ToastTone.Warning);
        });

        group.MapGet("set-role/{id:guid}", async (Guid id, IQuery<User> users, CancellationToken ct) =>
        {
            var row = await FindViewByIdAsync(users, id, ct);
            if (row is null) return Results.NotFound();
            return Result.ShowDialog(_SetRoleDialog.Create(new SetRoleDialogModel(row.Id, row.DisplayName, row.Email, row.Role)));
        });

        group.MapPost("set-role/{id:guid}", async (Guid id, [FromForm] string role, IAccountStore store, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(role) || !Enum.TryParse<UserRole>(role, ignoreCase: true, out var userRole) || !Enum.IsDefined(userRole))
            {
                return Results.BadRequest();
            }

            var user = await store.Users.FirstOrDefaultAsync(x => x.Id == id, ct);
            if (user is null) return Results.NotFound();

            user.Role = userRole;
            await store.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .ClearDialog()
                .WithOob($"#user-role-{id}", _RoleBadge.Create(new RoleBadgeModel(id, userRole)))
                .WithToast($"Role for '{user.DisplayName}' set to '{userRole}'.", title: "Role", tone: ToastTone.Success);
        });
    }

    private static async Task<UserListModel> CreateListModelAsync(IQuery<User> users, UserSearchModel search, UserListQuery query, CancellationToken ct)
    {
        var items = users.All()
            .Select(x => new UserRowModel
            {
                Id = x.Id,
                DisplayName = x.DisplayName,
                Email = x.Email,
                Provider = x.Provider,
                Role = x.Role,
                CreatedAtUtc = x.CreatedAtUtc,
                LastLoginAtUtc = x.LastLoginAtUtc,
                IsActive = x.IsActive
            });

        if (!string.IsNullOrWhiteSpace(search.Search))
        {
            var term = search.Search.Trim();
            items = items.Where(x =>
                x.DisplayName.Contains(term) ||
                x.Email.Contains(term) ||
                x.Provider.Contains(term));
        }

        var paged = await items.ToPagedListAsync(query, ct);
        return new UserListModel(paged.Total, search, query, paged.Items);
    }

    private static Task<UserViewModel?> FindViewByIdAsync(IQuery<User> users, Guid id, CancellationToken ct) =>
        users.All()
            .Where(x => x.Id == id)
            .Select(x => new UserViewModel(x.Id, x.DisplayName, x.Email, x.Provider, x.Role, x.CreatedAtUtc, x.LastLoginAtUtc, x.IsActive))
            .FirstOrDefaultAsync(ct);
}

