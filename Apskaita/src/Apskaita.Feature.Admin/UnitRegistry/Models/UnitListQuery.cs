using System.Linq.Expressions;

namespace Apskaita.Feature.Admin.UnitRegistry.Models;

public sealed partial class UnitListQuery : ListRequest<UnitRowModel>
{
    public override IReadOnlyDictionary<string, Expression<Func<UnitRowModel, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<UnitRowModel, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(UnitRowModel.Name)] = x => x.Name,
            [nameof(UnitRowModel.Code)] = x => x.Code,
            [nameof(UnitRowModel.IsActive)] = x => x.IsActive
        };

    public UnitListQuery()
    {
        SortBy = nameof(UnitRowModel.Name);
        Page = 1;
        PageSize = 10;
    }
}

