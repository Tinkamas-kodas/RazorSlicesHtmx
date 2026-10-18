using Apskaita.Entities.Contracts;
using Apskaita.Feature.WorkActs.Models;
using Apskaita.Feature.WorkActs.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Routing;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.WorkActs.Endpoints;

public sealed partial class WorkActEndpoints
{
    /// <summary>
    /// The list page (<c>""</c>) and its fragment (<c>list</c>). The page renders the filter plus the list
    /// fragment, which re-fetches itself on <c>work-acts-list-refresh</c>.
    /// </summary>
    private sealed class ListSection(WorkActEndpoints owner)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("", async (
                HttpContext ctx,
                [AsParameters] WorkActSearchModel search,
                WorkActListQuery query,
                IQuery<Project> projects,
                IQuery<WorkAct> acts,
                IQuery<InvoiceLine> invoiceLines,
                CancellationToken ct) =>
            {
                ApplyDefaultOrder(ctx.Request.Query, query);
                var model = await CreateListModelAsync(ctx, projects, acts, invoiceLines, search, query, ct);
                return owner.Result.Page(_ListPage.Create(model));
            });

            group.MapGet("list", async (
                HttpContext ctx,
                IFeatureUri uri,
                [AsParameters] WorkActSearchModel search,
                WorkActListQuery query,
                IQuery<Project> projects,
                IQuery<WorkAct> acts,
                IQuery<InvoiceLine> invoiceLines,
                CancellationToken ct) =>
            {
                if (!ctx.IsHtmx())
                {
                    return Results.Redirect(uri.Uri("") + ctx.Request.QueryString.Value, permanent: false);
                }

                ApplyDefaultOrder(ctx.Request.Query, query);
                var model = await CreateListModelAsync(ctx, projects, acts, invoiceLines, search, query, ct);
                return owner.Result.Fragment(_ActsList.Create(model));
            });
        }

        /// <summary>
        /// Newest period end first unless the request asked for a specific column or direction: the shared
        /// binder defaults a missing <c>SortDirection</c> to ascending, which would invert this list's
        /// intended order on a bare link.
        /// </summary>
        private static void ApplyDefaultOrder(IQueryCollection query, WorkActListQuery request)
        {
            if (string.IsNullOrWhiteSpace(query["SortBy"].ToString()) &&
                string.IsNullOrWhiteSpace(query["SortDirection"].ToString()))
            {
                request.SortBy = "periodEnd";
                request.SortDirection = SortDirection.Desc;
            }
        }
    }

    /// <summary>
    /// Filtered, sorted and paged acts plus the project filter options. Project names are joined in memory
    /// (the act table is small) so the sort and paging stay a single LINQ pipeline.
    /// </summary>
    private static async Task<WorkActListModel> CreateListModelAsync(
        HttpContext ctx,
        IQuery<Project> projects,
        IQuery<WorkAct> acts,
        IQuery<InvoiceLine> invoiceLines,
        WorkActSearchModel search,
        WorkActListQuery query,
        CancellationToken ct)
    {
        var displayOffset = BrowserTimeZone.GetDisplayOffset(ctx);

        var projectOptions = await projects.All()
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Code, Label = string.IsNullOrWhiteSpace(p.Code) ? p.Name : $"{p.Name} ({p.Code})" })
            .ToListAsync(ct);

        var selected = ParseProjectFilter(search.Projects);

        var rows = await acts.All()
            .OrderBy(x => x.Id)
            .Select(x => new
            {
                x.Id,
                x.ProjectId,
                x.PeriodStartUtc,
                x.PeriodEndUtc,
                x.TotalHours,
                x.TotalAmount,
                x.CurrencyCode,
                x.Status,
                x.CreatedAtUtc
            })
            .ToListAsync(ct);

        var projectLabels = projectOptions.ToDictionary(p => p.Id, p => p.Label);

        // A Final act that an invoice line references is presented as Invoiced (derived, no stored write).
        var invoicedIds = (await invoiceLines.All()
                .Where(l => l.WorkActId != null)
                .Select(l => l.WorkActId!.Value)
                .Distinct()
                .ToListAsync(ct))
            .ToHashSet();

        var all = rows
            .Where(r => selected.Count == 0 || selected.Contains(r.ProjectId))
            .Select(r => new WorkActRowModel(
                r.Id,
                projectLabels.TryGetValue(r.ProjectId, out var label) ? label : "(deleted project)",
                // The stored end is exclusive, so the last covered day is one tick before it.
                BrowserTimeZone.UtcToWall(r.PeriodStartUtc, displayOffset),
                BrowserTimeZone.UtcToWall(r.PeriodEndUtc, displayOffset).AddTicks(-1),
                r.TotalHours,
                r.TotalAmount,
                r.CurrencyCode,
                r.Status == WorkActStatus.Final && invoicedIds.Contains(r.Id) ? nameof(WorkActStatus.Invoiced) : r.Status.ToString(),
                r.Status == WorkActStatus.Draft,
                r.CreatedAtUtc))
            .AsQueryable();

        var paged = await all.ToPagedListAsync(query, ct);

        var options = projectOptions
            .Select(p => new WorkActProjectOption(p.Id, p.Code, p.Label, selected.Count == 0 || selected.Contains(p.Id)))
            .ToList();

        return new WorkActListModel(paged.Total, search, paged.Request, paged.Items, options);
    }

    /// <summary>Parses the comma-joined <c>Projects</c> query values into project ids, ignoring junk.</summary>
    private static HashSet<Guid> ParseProjectFilter(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var ids = new HashSet<Guid>();
        foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Guid.TryParse(part, out var id))
            {
                ids.Add(id);
            }
        }

        return ids;
    }

    /// <summary>
    /// Local date boundaries → UTC instants: the start is local midnight, the end the following local
    /// midnight and stays exclusive, so an entry starting exactly then belongs to the next period.
    /// </summary>
    private static (DateTime FromUtc, DateTime ToUtc) ToUtcBounds(DateOnly from, DateOnly to, TimeSpan displayOffset)
    {
        var startLocal = from.ToDateTime(TimeOnly.MinValue);
        var endLocal = to.ToDateTime(TimeOnly.MinValue).AddDays(1);

        return (BrowserTimeZone.WallToUtc(startLocal, displayOffset), BrowserTimeZone.WallToUtc(endLocal, displayOffset));
    }

    private static string ProjectLabelOf(string? code, string name) =>
        string.IsNullOrWhiteSpace(code) ? name : $"{name} ({code})";

    /// <summary>Line amount: hours (2 dp) at the effective rate, rounded to the currency's 2 decimals.</summary>
    private static decimal AmountOf(int durationMinutes, int hourlyRate) =>
        Math.Round(durationMinutes / 60m * hourlyRate, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// For each entry id, the labels of the <em>other</em> acts (project · period in the display zone)
    /// whose lines reference it. <paramref name="currentActId"/> is excluded; pass <see cref="Guid.Empty"/>
    /// on the review screen where no act exists yet.
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> OtherActsOfEntriesAsync(
        IQuery<WorkActLine> lines,
        IQuery<WorkAct> acts,
        IQuery<Project> projects,
        IReadOnlyCollection<Guid> entryIds,
        Guid currentActId,
        TimeSpan displayOffset,
        CancellationToken ct)
    {
        if (entryIds.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<string>>();
        }

        var ids = entryIds.ToArray();
        var query = lines.All().Where(l => ids.Contains(l.TimeTrackingId));
        if (currentActId != Guid.Empty)
        {
            query = query.Where(l => l.WorkActId != currentActId);
        }

        var rows = await (
            from line in query
            join act in acts.All() on line.WorkActId equals act.Id
            join project in projects.All() on act.ProjectId equals project.Id
            select new
            {
                line.TimeTrackingId,
                act.PeriodStartUtc,
                act.PeriodEndUtc,
                ProjectLabel = string.IsNullOrWhiteSpace(project.Code) ? project.Name : $"{project.Name} ({project.Code})"
            })
            .ToListAsync(ct);

        return rows
            .GroupBy(r => r.TimeTrackingId)
            .ToDictionary(
                g => g.Key,
                g => g
                    .Select(r => $"{r.ProjectLabel} · {DateOnly.FromDateTime(BrowserTimeZone.UtcToWall(r.PeriodStartUtc, displayOffset)):yyyy-MM-dd} → {DateOnly.FromDateTime(BrowserTimeZone.UtcToWall(r.PeriodEndUtc, displayOffset).AddTicks(-1)):yyyy-MM-dd}")
                    .Distinct()
                    .ToArray() as IReadOnlyList<string>);
    }

    /// <summary>
    /// The effective status label: a Final act that an invoice line references is presented as Invoiced;
    /// nothing writes the stored <c>Invoiced</c> value yet.
    /// </summary>
    private static async Task<string> EffectiveStatusLabelAsync(
        IQuery<InvoiceLine> invoiceLines,
        WorkAct act,
        CancellationToken ct) =>
        act.Status == WorkActStatus.Final && await invoiceLines.ExistsAsync(l => l.WorkActId == act.Id, ct)
            ? nameof(WorkActStatus.Invoiced)
            : act.Status.ToString();
}
