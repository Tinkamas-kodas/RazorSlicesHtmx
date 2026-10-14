using System.Globalization;
using Apskaita.Entities;
using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Models;
using Apskaita.Feature.TimeTracking.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Calendar;
using Apskaita.Shared.ProjectColors;
using Apskaita.Shared.Routing;
using Microsoft.EntityFrameworkCore;
using TimeTrackingEntry = Apskaita.Entities.TimeTracking;
using TimeTrackingModel = Apskaita.Feature.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Feature.TimeTracking.Endpoints;

public sealed partial class TimeTrackingEndpoints
{
    /// <summary>
    /// The week grid: the prefix-derived redirect and the list page/fragment. Calendar events are
    /// display-only enrichment (REQ-004/REQ-008) — the reader never throws.
    /// </summary>
    private sealed class WeekSection(TimeTrackingEndpoints owner)
    {
        private const int SlotMinutes = 30;
        private const double SlotHeightPx = 34d;
        private static readonly TimeOnly WorkDayStart = new(8, 0);
        private static readonly TimeOnly WorkDayEnd = new(17, 0);

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

            var days = Enumerable
                .Range(0, 7)
                .Select(offset =>
                {
                    var day = weekStart.AddDays(offset);
                    return new TimeTrackingDayModel(
                        day,
                        day.ToString("ddd dd.MM", CultureInfo.InvariantCulture),
                        day == todayDisplay);
                })
                .ToArray();

            var slots = Enumerable
                .Range(0, 48)
                .Select(index =>
                {
                    var start = TimeOnly.MinValue.AddMinutes(index * SlotMinutes);
                    var end = start.AddMinutes(SlotMinutes);
                    var isWorkHours = start >= WorkDayStart && start < WorkDayEnd;
                    return new TimeTrackingSlotModel(index, start, end, isWorkHours);
                })
                .ToArray();

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

            var entryViewModels = rows.Select(row => CreateEntryViewModel(row, displayOffset)).ToArray();

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

            var calendarEvents = (calendar?.Events ?? [])
                .Select(e => new TimeTrackingCalendarEventViewModel(
                    DateOnly.FromDateTime(e.StartLocal),
                    e.Subject,
                    TimeOnly.FromDateTime(e.StartLocal),
                    TimeOnly.FromDateTime(e.EndLocal),
                    e.Location,
                    e.IsOnline,
                    e.OrganizerName,
                    e.ProviderKey,
                    e.AccountKey,
                    e.EventId,
                    Math.Max(1, (int)Math.Round((e.EndLocal - e.StartLocal).TotalMinutes)),
                    trackedSet.Contains((e.ProviderKey.ToLowerInvariant(), e.AccountKey, e.EventId))))
                .ToArray();

            return new TimeTrackingPageModel(
                weekStart,
                weekStart.AddDays(6),
                weekStart.AddDays(-7),
                weekStart.AddDays(7),
                days,
                slots,
                entryViewModels,
                calendarEvents,
                calendar?.HasConnector == true,
                calendar?.Notice);
        }

        private static TimeTrackingEntryViewModel CreateEntryViewModel(TimeTrackingEntryRow row, TimeSpan displayOffset)
        {
            // Stored UTC → display-zone wall time; all grid math below works in wall time.
            var startedAtLocal = BrowserTimeZone.UtcToWall(row.StartedAtUtc, displayOffset);
            var durationMinutes = Math.Clamp(row.DurationInMinutes, TimeTrackingLimits.MinDurationMinutes, TimeTrackingLimits.MaxDurationMinutes);
            var entryEnd = startedAtLocal.AddMinutes(durationMinutes);
            var dayEnd = startedAtLocal.Date.AddDays(1);
            var visibleEnd = entryEnd <= dayEnd ? entryEnd : dayEnd;

            var visibleMinutes = Math.Max(1, (int)Math.Ceiling((visibleEnd - startedAtLocal).TotalMinutes));
            var overflowMinutes = Math.Max(0, (int)Math.Ceiling((entryEnd - dayEnd).TotalMinutes));
            var overflowHours = overflowMinutes <= 0
                ? 0m
                : (decimal)Math.Ceiling(overflowMinutes / 15d) / 4m;

            var minutesFromDayStart = (int)(startedAtLocal - startedAtLocal.Date).TotalMinutes;
            var startSlotIndex = Math.Clamp(minutesFromDayStart / SlotMinutes, 0, 47);
            var offsetInSlotMinutes = minutesFromDayStart % SlotMinutes;
            var offsetTopPx = offsetInSlotMinutes * (SlotHeightPx / SlotMinutes);
            var heightPx = visibleMinutes * (SlotHeightPx / SlotMinutes);
            var effectiveHeightPx = Math.Max(2d, heightPx);
            var descriptionLineClamp = ComputeDescriptionLineClamp(effectiveHeightPx);
            var colorPresentation = ProjectColorCatalog.CreatePresentation(row.ColorRgb);

            return new TimeTrackingEntryViewModel(
                row.Id,
                DateOnly.FromDateTime(startedAtLocal),
                startSlotIndex,
                TimeOnly.FromDateTime(startedAtLocal),
                TimeOnly.FromDateTime(entryEnd),
                string.IsNullOrWhiteSpace(row.Description) ? "No description" : row.Description.Trim(),
                colorPresentation.BackgroundColorRgb,
                colorPresentation.AccentColorRgb,
                colorPresentation.TextColorRgb,
                offsetTopPx,
                effectiveHeightPx,
                overflowHours,
                descriptionLineClamp);
        }

        private static int ComputeDescriptionLineClamp(double cardHeightPx)
        {
            const double reservedPx = 24d;
            const double lineHeightPx = 14d;
            var lines = (int)Math.Floor((cardHeightPx - reservedPx) / lineHeightPx);
            return Math.Clamp(lines, 1, 24);
        }

        private sealed record TimeTrackingEntryRow(
            Guid Id,
            DateTime StartedAtUtc,
            int DurationInMinutes,
            string Description,
            string ColorRgb);
    }
}
