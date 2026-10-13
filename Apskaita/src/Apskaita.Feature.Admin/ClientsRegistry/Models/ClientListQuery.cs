using System.Linq.Expressions;

namespace Apskaita.Web.Features.Admin.ClientsRegistry.Models;

public sealed partial class ClientListQuery : ListRequest<ClientRowModel>
{
    public override IReadOnlyDictionary<string, Expression<Func<ClientRowModel, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<ClientRowModel, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ClientRowModel.Name)] = x => x.Name,
            [nameof(ClientRowModel.CompanyCode)] = x => x.CompanyCode!,
            [nameof(ClientRowModel.VatCode)] = x => x.VatCode!,
            [nameof(ClientRowModel.ContactEmail)] = x => x.ContactEmail!,
            [nameof(ClientRowModel.IsActive)] = x => x.IsActive
        };

    private const string DefaultSortBy = nameof(ClientRowModel.Name);
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;

    public ClientListQuery()
    {
        SortBy = DefaultSortBy;
        Page = DefaultPage;
        PageSize = DefaultPageSize;
    }
}
