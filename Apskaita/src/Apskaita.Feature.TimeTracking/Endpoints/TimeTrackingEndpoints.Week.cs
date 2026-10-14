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
    /// The week grid: the prefix-derived redirect and the list page/fragment. Calendar events are
    /// display-only enrichment (REQ-004/REQ-008) — the reader never throws. Geometry and card math
    /// live in <see cref="TimeTrackingGridGeometry"/> and the view-model factories.
    /// </summary>
    private sealed class WeekSection(TimeTrackingEndpoints owner)
    {
        public void Map(RouteGroupBuilder group)
        {
            // Prefix-derived redirect: IFeatureUri resolves the registered route group for this request,
            // so the target follows wherever the feature is mounted (query string preserved).
            group.MapGet("", (IFeatureUri uri, HttpContext ctx) =>
                Results.Redirect(uri.Uri("list") + ctx.Request.QueryString.Value, permanent: false));

            group.MapGet("list", async (
                HttpContext ctx,
                string? weekStart,
                IQuery<TimeTrackingEntry> entries,
                IQuery<Project> projects,
                ICalendarEventReader calendarReader,
                CancellationToken ct) =>
            {
                var (weekStartDay, weekEndDay) = TimeTrackingModel.CreateWeekBounds(weekStart);
                var displayOffset = BrowserTimeZone.GetDisplayOffset(ctx);
                var calendar = await calendarReader.ReadAsync(weekStartDay, weekEndDay, ct);
                var model = await CreatePageModelAsync(entries, projects, weekStart, calendar, displayOffset, ct);

                if (ctx.IsHtmxTarget("time-tracking-grid"))
                {
                    return owner.Result.Fragment(_Grid.Create(model));
                }

                return owner.Result.Page(_ListPage.Create(model));
            });
        }

        private static async Task<TimeTrackingPageModel> CreatePageModelAsync(
            IQuery<TimeTrackingEntry> entries,
            IQuery<Project> projects,
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

            // References of entries in the displayed week: an event whose identity triple matches is
            // "tracked" (badge); the unique index guarantees at most one entry per event.
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
                rows.Select(row => TimeTrackingEntryViewModel.From(row, displayOffset)).ToArray(),
                (calendar?.Events ?? [])
                    .Select(e => TimeTrackingCalendarEventViewModel.From(
                        e,
                        trackedSet.Contains((e.ProviderKey.ToLowerInvariant(), e.AccountKey, e.EventId))))
                    .ToArray(),
                calendar?.HasConnector == true,
                calendar?.Notice);
        }
    }
}
