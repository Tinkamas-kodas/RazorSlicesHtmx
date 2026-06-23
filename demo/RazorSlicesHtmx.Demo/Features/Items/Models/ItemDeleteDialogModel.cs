namespace razr_slices_htmx2.Features.Items.Models;

public sealed record ItemDeleteDialogModel(
    int Id,
    string Code,
    string Name,
    string PostUrl,
    ItemListQuery Query);