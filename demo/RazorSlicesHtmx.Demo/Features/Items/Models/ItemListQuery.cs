namespace RazorSlicesHtmx.Demo.Features.Items.Models;

public sealed partial class ItemListQuery : ListRequest<ItemRowModel>
{
    private const string DefaultSortBy = "code";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 5;

    public ItemListQuery()
    {
        SortBy = DefaultSortBy;
        Page = DefaultPage;
        PageSize = DefaultPageSize;
    }
}
