namespace RshtmxApp.Features.Items.Models;

public sealed record ItemRowModel(
    [property: SortDisable] int Id,
    string Name,
    string Description,
    bool IsActive);
