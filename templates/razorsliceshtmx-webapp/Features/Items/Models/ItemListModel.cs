namespace RshtmxApp.Features.Items.Models;

public sealed class ItemListModel
{
    public required ListResponse<ItemRow, ItemListQuery> Response { get; init; }

    public required ItemSearch Search { get; init; }
}
