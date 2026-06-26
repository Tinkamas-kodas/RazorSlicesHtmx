namespace RshtmxApp.Features.Items.Models;

public sealed record ItemListModel(int Total, ItemSearchModel Search, ItemListQuery Request, List<ItemRowModel> Items)
    : ListResponse<ItemRowModel, ItemListQuery>(Total, Request, Items);
