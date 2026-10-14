namespace Apskaita.Feature.Admin.UnitRegistry.Models;

public sealed record UnitListModel(int Total, UnitSearchModel Search, UnitListQuery Request, List<UnitRowModel> Items)
    : ListResponse<UnitRowModel, UnitListQuery>(Total, Request, Items);

