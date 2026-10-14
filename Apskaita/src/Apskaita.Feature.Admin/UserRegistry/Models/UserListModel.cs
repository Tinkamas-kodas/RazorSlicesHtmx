namespace Apskaita.Feature.Admin.UserRegistry.Models;

public sealed record UserListModel(int Total, UserSearchModel Search, UserListQuery Request, List<UserRowModel> Items)
    : ListResponse<UserRowModel, UserListQuery>(Total, Request, Items);

