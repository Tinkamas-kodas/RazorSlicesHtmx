using System.Linq.Expressions;

namespace Apskaita.Feature.Admin.IssuerRegistry.Models;

public sealed partial class IssuerListQuery : ListRequest<IssuerRowModel>
{
    public override IReadOnlyDictionary<string, Expression<Func<IssuerRowModel, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<IssuerRowModel, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(IssuerRowModel.Name)] = x => x.Name,
            [nameof(IssuerRowModel.CompanyCode)] = x => x.CompanyCode!,
            [nameof(IssuerRowModel.VatCode)] = x => x.VatCode!,
            [nameof(IssuerRowModel.ContactEmail)] = x => x.ContactEmail!,
            [nameof(IssuerRowModel.IsActive)] = x => x.IsActive
        };

    private const string DefaultSortBy = nameof(IssuerRowModel.Name);
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;

    public IssuerListQuery()
    {
        SortBy = DefaultSortBy;
        Page = DefaultPage;
        PageSize = DefaultPageSize;
    }
}

