namespace razr_slices_htmx2.Features.Items.Models;

public sealed class Item
{
    public int Id { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public bool IsEnabled { get; set; }
}