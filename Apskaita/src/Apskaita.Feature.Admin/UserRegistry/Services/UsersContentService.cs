using Apskaita.Feature.Admin.UserRegistry.Models;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.Admin.UserRegistry.Services;

public sealed class UsersContentService
{
    public async Task<UserListModel> CreateListModelAsync(ApskaitaDbContext db, UserSearchModel search, UserListQuery query, CancellationToken ct)
    {
        var items = db.Set<User>().AsNoTracking()
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

    public async Task<UserViewModel?> FindViewByIdAsync(ApskaitaDbContext db, Guid id, CancellationToken ct)
    {
        return await db.Set<User>().AsNoTracking()
            .Where(x => x.Id == id)
            .Select(x => new UserViewModel(x.Id, x.DisplayName, x.Email, x.Provider, x.Role, x.CreatedAtUtc, x.LastLoginAtUtc, x.IsActive))
            .FirstOrDefaultAsync(ct);
    }

    public Task<User?> FindByIdAsync(ApskaitaDbContext db, Guid id, CancellationToken ct) =>
        db.Set<User>().FirstOrDefaultAsync(x => x.Id == id, ct);

    public async Task ToggleActiveAsync(ApskaitaDbContext db, User user, CancellationToken ct)
    {
        user.IsActive = !user.IsActive;
        await db.SaveChangesAsync(ct);
    }

    public async Task SetRoleAsync(ApskaitaDbContext db, User user, UserRole? role, CancellationToken ct)
    {
        user.Role = role;
        await db.SaveChangesAsync(ct);
    }
}

