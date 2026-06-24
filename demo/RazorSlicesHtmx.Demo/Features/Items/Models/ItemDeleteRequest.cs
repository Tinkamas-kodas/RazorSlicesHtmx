namespace RazorSlicesHtmx.Demo.Features.Items.Models;

public sealed class ItemDeleteRequest
{
    public int Id { get; set; }

    public string? Search { get; set; }

    public string? SortBy { get; set; }

    public string? SortDir { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 5;
}