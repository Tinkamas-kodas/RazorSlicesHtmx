namespace RazorSlicesHtmx.AspNetCore.Models;

public interface IListRequest
{
    int Page { get; set; }

    int PageSize { get; set; }

    string? SortBy { get; set; }

    SortDirection SortDirection { get; set; }
}

