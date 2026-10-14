using System.Linq.Expressions;

namespace Apskaita.Feature.Admin.UserRegistry.Models;

public sealed partial class UserListQuery : ListRequest<UserRowModel>
{
    public override IReadOnlyDictionary<string, Expression<Func<UserRowModel, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<UserRowModel, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(UserRowModel.DisplayName)] = x => x.DisplayName,
            [nameof(UserRowModel.Email)] = x => x.Email,
            [nameof(UserRowModel.Provider)] = x => x.Provider,
            [nameof(UserRowModel.Role)] = x => x.Role!,
            [nameof(UserRowModel.CreatedAtUtc)] = x => x.CreatedAtUtc,
            [nameof(UserRowModel.LastLoginAtUtc)] = x => x.LastLoginAtUtc!,
            [nameof(UserRowModel.IsActive)] = x => x.IsActive
        };

    public UserListQuery()
    {
        SortBy = nameof(UserRowModel.DisplayName);
        Page = 1;
        PageSize = 10;
    }
}

