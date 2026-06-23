namespace razr_slices_htmx2.Features.Items.Models;

public sealed class ItemUpsertRequest
{
    public int? Id { get; set; }

    public string? Code { get; set; }

    public string? Name { get; set; }

    public bool IsEnabled { get; set; }

    public string? Search { get; set; }

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 5;
}