namespace Apskaita.Web.Features.Admin.IssuerRegistry.Models;

public sealed record IssuerListModel(int Total, IssuerSearchModel Search, IssuerListQuery Request, List<IssuerRowModel> Items)
    : ListResponse<IssuerRowModel, IssuerListQuery>(Total, Request, Items);

