namespace RazorSlicesHtmx.Demo.Features.Items.Models;

public sealed record ItemRowModel(
    int Id,
    string Code,
    string Name,
    bool IsEnabled,
    string EditUrl,
    string DeleteUrl);