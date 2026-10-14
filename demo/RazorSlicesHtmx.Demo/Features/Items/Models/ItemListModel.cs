namespace RazorSlicesHtmx.Demo.Features.Items.Models;

[RazorSlicesHtmx.AspNetCore.Html.GenerateHtmlNames]
public sealed record ItemListModel(int Total, ItemSearchModel Search, ItemListQuery Request, List<ItemRowModel> Items)
    : ListResponse<ItemRowModel, ItemListQuery>(Total, Request, Items);