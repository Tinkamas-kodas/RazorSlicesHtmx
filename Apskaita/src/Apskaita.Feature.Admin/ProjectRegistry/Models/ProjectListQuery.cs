using System.Linq.Expressions;

namespace Apskaita.Feature.Admin.ProjectRegistry.Models;

public sealed partial class ProjectListQuery : ListRequest<ProjectRowModel>
{
    public override IReadOnlyDictionary<string, Expression<Func<ProjectRowModel, object?>>> SortMap { get; } =
        new Dictionary<string, Expression<Func<ProjectRowModel, object?>>>(StringComparer.OrdinalIgnoreCase)
        {
            [nameof(ProjectRowModel.Name)] = x => x.Name,
            [nameof(ProjectRowModel.Code)] = x => x.Code!,
            [nameof(ProjectRowModel.ClientName)] = x => x.ClientName,
            [nameof(ProjectRowModel.HourlyRate)] = x => x.HourlyRate,
            [nameof(ProjectRowModel.CurrencyCode)] = x => x.CurrencyCode,
            [nameof(ProjectRowModel.TemplateName)] = x => x.TemplateName,
            [nameof(ProjectRowModel.IsActive)] = x => x.IsActive
        };

    public ProjectListQuery()
    {
        SortBy = nameof(ProjectRowModel.Name);
        Page = 1;
        PageSize = 10;
    }
}

