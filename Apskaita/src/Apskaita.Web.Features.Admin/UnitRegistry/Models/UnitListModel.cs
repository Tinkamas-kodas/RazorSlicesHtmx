namespace Apskaita.Web.Features.Admin.UnitRegistry.Models;

public sealed record UnitListModel(int Total, UnitSearchModel Search, UnitListQuery Request, List<UnitRowModel> Items)
    : ListResponse<UnitRowModel, UnitListQuery>(Total, Request, Items);

