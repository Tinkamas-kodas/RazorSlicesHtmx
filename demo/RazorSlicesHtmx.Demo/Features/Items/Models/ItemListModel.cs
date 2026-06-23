namespace razr_slices_htmx2.Features.Items.Models;

public sealed record ItemListModel(
    ItemListQuery Query,
    IReadOnlyList<ItemRowModel> Rows,
    int TotalCount,
    int TotalPages,
    int CurrentPage,
    int FromItem,
    int ToItem);