using Apskaita.Entities;

namespace Apskaita.Feature.WorkActs.Models;

/// <summary>
/// One row of the work-acts list. The period is carried as display-zone wall dates (converted from the
/// stored UTC instants by the endpoint) so the row shows the boundaries the user originally chose.
/// </summary>
public sealed record WorkActRowModel(
    Guid Id,
    string ProjectLabel,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    decimal TotalHours,
    decimal TotalAmount,
    string CurrencyCode,
    string StatusLabel,
    bool IsDraft,
    DateTime CreatedAtUtc);

/// <summary>List model for the work-acts grid.</summary>
public sealed record WorkActListModel(
    int Total,
    WorkActSearchModel Search,
    WorkActListQuery Request,
    List<WorkActRowModel> Items,
    IReadOnlyList<WorkActProjectOption> Projects)
    : ListResponse<WorkActRowModel, WorkActListQuery>(Total, Request, Items);

/// <summary>
/// Filter over the list. <see cref="Projects"/> arrives as repeated query values (one per selected
/// project) and binds to a comma-joined string, which keeps the state serializer simple.
/// </summary>
public sealed partial record WorkActSearchModel(string Projects = "");

/// <summary>
/// One option of the project filter or create dialog; <see cref="IsSelected"/> reflects the active filter.
/// <see cref="Code"/> lets the create dialog link to the review page by project code.
/// </summary>
public sealed record WorkActProjectOption(Guid Id, string Code, string Label, bool IsSelected);

/// <summary>The create dialog: the active projects to choose from plus the form values to redisplay.</summary>
public sealed record WorkActCreateDialogModel(
    IReadOnlyList<WorkActProjectOption> Projects,
    WorkActCreateRequest Request);

/// <summary>The review/edit screen's totals line, re-rendered by OOB after a row save.</summary>
public sealed record WorkActTotalsModel(decimal Hours, decimal Amount, string CurrencyCode);

/// <summary>What the delete confirmation shows: the act is identified by project and period, not a number.</summary>
public sealed record WorkActDeleteModel(Guid Id, string ProjectLabel, DateTime PeriodStart, DateTime PeriodEnd);

/// <summary>Sorting and paging for the work-acts list; the generator supplies <c>SortMap</c> and the binder.</summary>
public sealed partial class WorkActListQuery : ListRequest<WorkActRowModel>
{
    private const string DefaultSortBy = "periodEnd";
    private const int DefaultPage = 1;
    private const int DefaultPageSize = 10;

    public WorkActListQuery()
    {
        SortBy = DefaultSortBy;
        SortDirection = SortDirection.Desc;
        Page = DefaultPage;
        PageSize = DefaultPageSize;
    }
}
