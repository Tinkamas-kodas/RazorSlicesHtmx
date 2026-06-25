using System.Linq.Expressions;

namespace RazorSlicesHtmx.Demo.Features.Items.Models;

public sealed record ItemRowModel(
    [property: SortDisable] int Id,
    string Code,
    string Name,
    [property: CustomSortExpression(nameof(ItemRowModel.IsEnabledSortExpression))] bool IsEnabled)
{
    public static Expression<Func<ItemRowModel, object?>> IsEnabledSortExpression() => row => !row.IsEnabled;

}