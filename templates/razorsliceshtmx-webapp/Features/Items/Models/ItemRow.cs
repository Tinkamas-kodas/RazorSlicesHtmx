namespace RshtmxApp.Features.Items.Models;

[GenerateHtmlNames]
public sealed class ItemRow
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
