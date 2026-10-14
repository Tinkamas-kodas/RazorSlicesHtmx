namespace Apskaita.Feature.Admin.ClientsRegistry.Models;

public sealed record ClientListModel(int Total, ClientSearchModel Search, ClientListQuery Request, List<ClientRowModel> Items)
    : ListResponse<ClientRowModel, ClientListQuery>(Total, Request, Items);
