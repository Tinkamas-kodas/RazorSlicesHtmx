using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Models;
using Apskaita.Feature.TimeTracking.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Calendar;
using Apskaita.Shared.Routing;
using Microsoft.EntityFrameworkCore;
using TimeTrackingEntry = Apskaita.Entities.TimeTracking;
using TimeTrackingModel = Apskaita.Feature.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Feature.TimeTracking.Endpoints;

public sealed partial class TimeTrackingEndpoints
{
    /// <summary>
    /// The week grid: the list page (<c>""</c>) and the grid fragment (<c>list</c>). The page renders the
    /// controls plus an empty grid placeholder that pulls <c>list</c> on <c>hx-trigger="load"</c> and on
    /// every <c>time-tracking-list-refresh</c>, so a page request never touches entries or Graph. Calendar
    /// events are display-only enrichment (REQ-004/REQ-008) — the reader never throws. Geometry and card
    /// math live in <see cref="TimeTrackingGridGeometry"/> and the view-model factories.
    /// </summary>
    private sealed class WeekSection(TimeTrackingEndpoints owner)
    {
        public void Map(RouteGroupBuilder group)
        {
            // The list page (shell + controls + grid placeholder): Result.Page renders the shell for a
            // full request and the article fragment when the sidebar hx-gets this route.
            group.MapGet("", (string? weekStart) =>
            {
                var model = CreatePageShellModel(weekStart);
                return owner.Result.Page(_ListPage.Create(model));
            });

            group.MapGet("list", async (
                HttpContext ctx,
                IFeatureUri uri,
                string? weekStart,
                IQuery<TimeTrackingEntry> entries,
                IQuery<Project> projects,
                IQuery<WorkActLine> actLines,
                ICalendarEventReader calendarReader,
                CancellationToken ct) =>
            {
                // Direct navigation or history restore of a pushed list URL gets the page; the grid
                // arrives from the page's own hx-get (query string preserved).
                if (!ctx.IsHtmx())
                {
                    return Results.Redirect(uri.Uri("") + ctx.Request.QueryString.Value, permanent: false);
                }

                var (weekStartDay, weekEndDay) = TimeTrackingModel.CreateWeekBounds(weekStart);
                var displayOffset = BrowserTimeZone.GetDisplayOffset(ctx);
                var calendar = await calendarReader.ReadAsync(weekStartDay, weekEndDay, ct);
                var model = await CreatePageModelAsync(entries, projects, actLines, weekStart, calendar, displayOffset, ct);

                return owner.Result.Fragment(_Grid.Create(model));
            });
        }

        /// <summary>
        /// Page-shell model for the <c>""</c> route: week dates and navigation only. The grid placeholder
        /// loads its own content from <c>list</c>, so this request reads no entries, no tracked
        /// references, and no calendar.
        /// </summary>
        private static TimeTrackingPageModel CreatePageShellModel(string? weekStartRaw)
        {
            var weekStart = TimeTrackingModel.GetWeekStart(TimeTrackingModel.ResolveAnchorDate(weekStartRaw));

            return new TimeTrackingPageModel(
                weekStart,
                weekStart.AddDays(6),
                weekStart.AddDays(-7),
                weekStart.AddDays(7),
                [],
                [],
                [],
                [],
                false,
                null);
        }

        private static async Task<TimeTrackingPageModel> CreatePageModelAsync(
            IQuery<TimeTrackingEntry> entries,
            IQuery<Project> projects,
            IQuery<WorkActLine> actLines,
            string? weekStartRaw,
            CalendarReadResult? calendar,
            TimeSpan displayOffset,
            CancellationToken ct)
        {
            var anchorDate = TimeTrackingModel.ResolveAnchorDate(weekStartRaw);
            var weekStart = TimeTrackingModel.GetWeekStart(anchorDate);
            var weekStartLocal = weekStart.ToDateTime(TimeOnly.MinValue);
            var weekEndExclusiveLocal = weekStartLocal.AddDays(7);
            // Entries are stored as UTC; the week is defined in the display zone, so the query bounds
            // shift from display wall time to UTC (utc = wall − offset).
            var weekStartUtc = BrowserTimeZone.WallToUtc(weekStartLocal, displayOffset);
            var weekEndExclusiveUtc = BrowserTimeZone.WallToUtc(weekEndExclusiveLocal, displayOffset);
            var todayDisplay = DateOnly.FromDateTime(BrowserTimeZone.UtcToWall(DateTime.UtcNow, displayOffset));

            var rows = await (
                from time in entries.All()
                join project in projects.All() on time.ProjectId equals project.Id
                where time.StartedAtUtc >= weekStartUtc && time.StartedAtUtc < weekEndExclusiveUtc
                orderby time.StartedAtUtc
                select new TimeTrackingEntryRow(
                    time.Id,
                    time.StartedAtUtc,
                    time.DurationInMinutes,
                    time.Description,
                    project.ColorRgb)
                )
                .ToListAsync(ct);

            // An entry referenced by a work act line is locked; the grid marks those cards.
            var rowIds = rows.Select(r => r.Id).ToArray();
            var lockedIds = (await actLines.All()
                    .Where(l => rowIds.Contains(l.TimeTrackingId))
                    .Select(l => l.TimeTrackingId)
                    .Distinct()
                    .ToListAsync(ct))
                .ToHashSet();
            var gridRows = rows
                .Select(r => lockedIds.Contains(r.Id) ? r with { InWorkAct = true } : r)
                .ToArray();

            // References of entries in the displayed week: an event whose identity triple matches is
            // already represented by that entry, so it is hidden from the grid (the unique index guarantees
            // at most one entry per event). Global correctness is the event endpoint's job, not the grid's.
            var trackedRefs = await (
                from time in entries.All()
                where time.CalendarEvent != null && time.StartedAtUtc >= weekStartUtc && time.StartedAtUtc < weekEndExclusiveUtc
                select new
                {
                    time.CalendarEvent!.CalendarProvider,
                    time.CalendarEvent.AccountKey,
                    time.CalendarEvent.ExternalEventId
                })
                .ToListAsync(ct);
            var trackedSet = trackedRefs
                .Select(t => (t.CalendarProvider.ToString().ToLowerInvariant(), t.AccountKey, t.ExternalEventId))
                .ToHashSet();

            return new TimeTrackingPageModel(
                weekStart,
                weekStart.AddDays(6),
                weekStart.AddDays(-7),
                weekStart.AddDays(7),
                TimeTrackingDayModel.WeekOf(weekStart, todayDisplay),
                TimeTrackingSlotModel.WorkSlots(),
                gridRows.Select(row => TimeTrackingEntryViewModel.From(row, displayOffset)).ToArray(),
                (calendar?.Events ?? [])
                    .Where(e => !trackedSet.Contains((e.ProviderKey.ToLowerInvariant(), e.AccountKey, e.EventId)))
                    .Select(TimeTrackingCalendarEventViewModel.From)
                    .ToArray(),
                calendar?.HasConnector == true,
                calendar?.Notice);
        }
    }
}
