namespace razr_slices_htmx2.Features.Items.Models;

public sealed record ItemRowModel(
    int Id,
    string Code,
    string Name,
    bool IsEnabled,
    string EditUrl,
    string DeleteUrl);