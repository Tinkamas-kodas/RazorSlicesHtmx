namespace Apskaita.Web.Features.Admin.ProjectRegistry.Models;

public sealed record ProjectListModel(int Total, ProjectSearchModel Search, ProjectListQuery Request, List<ProjectRowModel> Items)
    : ListResponse<ProjectRowModel, ProjectListQuery>(Total, Request, Items);

