using System.Globalization;
using Apskaita.Entities;
using Apskaita.Web.Features.TimeTracking.Models;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.Calendar;
using Apskaita.Web.Shared.ProjectColors;
using Microsoft.EntityFrameworkCore;
using TimeTrackingModel = Apskaita.Web.Features.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Web.Features.TimeTracking.Services;

public sealed class TimeTrackingContentService
{
    private const int DefaultDurationMinutes = TimeTrackingLimits.DefaultDurationMinutes;
    private const int MinimumDurationMinutes = TimeTrackingLimits.MinDurationMinutes;
    private const int SlotMinutes = 30;
    private const double SlotHeightPx = 34d;
    private const int MaxOccurrences = TimeTrackingLimits.MaxOccurrences;
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
    private static readonly TimeOnly WorkDayStart = new(8, 0);
    private static readonly TimeOnly WorkDayEnd = new(17, 0);
    private static readonly TimeOnly DefaultSlotStart = new(8, 0);

    public async Task<TimeTrackingPageModel> CreatePageModelAsync(
        ApskaitaDbContext db,
        string? weekStartRaw,
        CalendarReadResult? calendar,
        TimeSpan displayOffset,
        CancellationToken ct)
    {
        var anchorDate = ResolveAnchorDate(weekStartRaw);
        var weekStart = GetWeekStart(anchorDate);
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
            from time in db.TimeTrackings.AsNoTracking()
            join project in db.Projects.AsNoTracking() on time.ProjectId equals project.Id
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

        var entries = rows.Select(row => CreateEntryViewModel(row, displayOffset)).ToArray();

        // References of entries in the displayed week: an event whose identity triple matches is
        // "tracked" (badge); the unique index guarantees at most one entry per event.
        var trackedRefs = await (
            from time in db.TimeTrackings.AsNoTracking()
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
            entries,
            calendarEvents,
            calendar?.HasConnector == true,
            calendar?.Notice);
    }

    public TimeTrackingModel CreateCreateDialogModel(string? dateRaw, string? timeRaw, string? weekStartRaw)
    {
        var weekStart = GetWeekStart(ResolveAnchorDate(weekStartRaw));
        var selectedDate = DateOnly.TryParse(dateRaw, out var date)
            ? date
            : weekStart;

        var start = TimeOnly.TryParse(timeRaw, out var parsedStart)
            ? parsedStart
            : DefaultSlotStart;

        return new TimeTrackingModel
        {
            Date = selectedDate,
            Start = start,
            DurationMinutes = DefaultDurationMinutes,
            HourlyRate = null,
            ProjectId = null,
            ProjectLabel = null,
            Description = null,
            Errors = EmptyErrors
        };
    }

    public TimeTrackingModel CreateCreateDialogModel(
        TimeTrackingModel request,
        IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> errors)
    {
        var selectedDate = request.Date == default
            ? DateOnly.FromDateTime(DateTime.Now)
            : request.Date;

        var start = request.Start == default
            ? DefaultSlotStart
            : request.Start;

        var durationMinutes = request.DurationMinutes;
        if (durationMinutes < MinimumDurationMinutes)
        {
            durationMinutes = DefaultDurationMinutes;
        }

        return new TimeTrackingModel
        {
            Date = selectedDate,
            Start = start,
            DurationMinutes = durationMinutes,
            HourlyRate = request.HourlyRate,
            ProjectId = request.ProjectId,
            ProjectLabel = string.IsNullOrWhiteSpace(request.ProjectLabel) ? null : request.ProjectLabel.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            Repeat = request.Repeat,
            RepeatEveryWeeks = request.RepeatEveryWeeks,
            RepeatUntil = request.RepeatUntil,
            EventProviderKey = request.EventProviderKey,
            EventAccountKey = request.EventAccountKey,
            EventId = request.EventId,
            FromCalendarEvent = !string.IsNullOrEmpty(request.EventProviderKey)
                || !string.IsNullOrEmpty(request.EventAccountKey)
                || !string.IsNullOrEmpty(request.EventId),
            Errors = errors
        };
    }

    /// <summary>
    /// Create-dialog model for the <c>event</c> endpoint's miss path: prefilled with the event's
    /// display-zone start and duration plus the identity triple carried as hidden fields.
    /// </summary>
    public TimeTrackingModel CreateEventDialogModel(
        string? dateRaw,
        string? timeRaw,
        int? durationMinutes,
        string? weekStartRaw,
        string providerKey,
        string accountKey,
        string eventId)
    {
        var weekStart = GetWeekStart(ResolveAnchorDate(weekStartRaw));
        var selectedDate = DateOnly.TryParse(dateRaw, out var date) ? date : weekStart;
        var start = TimeOnly.TryParse(timeRaw, out var parsedStart) ? parsedStart : DefaultSlotStart;

        return new TimeTrackingModel
        {
            Date = selectedDate,
            Start = start,
            DurationMinutes = Math.Clamp(durationMinutes ?? DefaultDurationMinutes, MinimumDurationMinutes, TimeTrackingLimits.MaxDurationMinutes),
            HourlyRate = null,
            ProjectId = null,
            ProjectLabel = null,
            Description = null,
            Repeat = false,
            EventProviderKey = providerKey,
            EventAccountKey = accountKey,
            EventId = eventId,
            FromCalendarEvent = true,
            Errors = EmptyErrors
        };
    }

    public async Task<TimeTrackingModel?> CreateEditDialogModelAsync(
        ApskaitaDbContext db,
        Guid id,
        TimeSpan displayOffset,
        CancellationToken ct)
    {
        var row = await (
            from time in db.TimeTrackings.AsNoTracking()
            join project in db.Projects.AsNoTracking() on time.ProjectId equals project.Id
            where time.Id == id
            select new
            {
                time.StartedAtUtc,
                time.DurationInMinutes,
                time.HourlyRate,
                time.ProjectId,
                ProjectLabel = project.Code == null ? project.Name : $"{project.Name} ({project.Code})",
                time.Description
            })
            .SingleOrDefaultAsync(ct);

        if (row is null)
        {
            return null;
        }

        // The dialog edits wall-clock time in the display zone; shift the stored UTC value back.
        var startedAtLocal = BrowserTimeZone.UtcToWall(row.StartedAtUtc, displayOffset);

        return new TimeTrackingModel
        {
            Date = DateOnly.FromDateTime(startedAtLocal),
            Start = TimeOnly.FromDateTime(startedAtLocal),
            DurationMinutes = row.DurationInMinutes,
            HourlyRate = row.HourlyRate,
            ProjectId = row.ProjectId,
            ProjectLabel = row.ProjectLabel,
            Description = row.Description,
            Errors = EmptyErrors
        };
    }

    public async Task<Dictionary<Guid, string?>> CreateProjectLookupModelAsync(ApskaitaDbContext db, string? query, CancellationToken ct)
    {
        var term = query?.Trim();

        var projects = db.Projects
            .AsNoTracking()
            .Where(project => project.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            projects = projects.Where(project =>
                project.Name.Contains(term) ||
                (project.Code != null && project.Code.Contains(term)));
        }

        var items = await projects
            .OrderBy(project => project.Name)
            .Take(20)
            .Select(project => new
            {
                project.Id,
                Label = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
            })
            .ToDictionaryAsync(x => x.Id, x => (string?)x.Label, ct);

        return items;
    }

    public string CreateSavedMessage(TimeTrackingModel request, string projectLabel)
    {
        var duration = request.DurationMinutes;
        if (duration < MinimumDurationMinutes)
        {
            duration = DefaultDurationMinutes;
        }
        var description = string.IsNullOrWhiteSpace(request.Description)
            ? "no description"
            : request.Description.Trim();

        return $"Logged {duration} min for '{projectLabel}' ({description}).";
    }

    public string CreateSavedMessage(TimeTrackingModel request, string projectLabel, int count)
    {
        if (count <= 1)
        {
            return CreateSavedMessage(request, projectLabel);
        }

        var description = string.IsNullOrWhiteSpace(request.Description)
            ? "no description"
            : request.Description.Trim();

        return $"Created {count} recurring entries for '{projectLabel}' ({description}).";
    }

    public IReadOnlyList<DateTime> CreateOccurrences(TimeTrackingModel request)
    {
        var baseStarted = ResolveStartedAtLocal(request);
        if (!request.Repeat || !request.RepeatUntil.HasValue)
        {
            return [baseStarted];
        }

        var stepDays = Math.Max(TimeTrackingLimits.MinRepeatWeeks, request.RepeatEveryWeeks ?? TimeTrackingLimits.MinRepeatWeeks) * TimeTrackingLimits.DaysPerWeek;
        var baseTime = TimeOnly.FromDateTime(baseStarted);
        var baseDate = DateOnly.FromDateTime(baseStarted);
        var results = new List<DateTime> { baseStarted };

        for (var i = 1; i < MaxOccurrences; i++)
        {
            var nextDate = baseDate.AddDays(i * stepDays);
            if (request.RepeatUntil.HasValue && nextDate > request.RepeatUntil.Value)
            {
                break;
            }

            results.Add(nextDate.ToDateTime(baseTime));
        }

        return results;
    }

    /// <summary>Resolves the dialog's date+start into a display-zone wall time; callers convert it to UTC before storing.</summary>
    public DateTime ResolveStartedAtLocal(TimeTrackingModel request)
    {
        var date = request.Date == default ? DateOnly.FromDateTime(DateTime.Now) : request.Date;
        var start = request.Start == default ? DefaultSlotStart : request.Start;

        return date.ToDateTime(start);
    }

    /// <summary>
    /// Validates a calendar event reference triple (REQ-004): provider key parses case-insensitively,
    /// keys are non-blank and within <see cref="CalendarEventReference.MaxKeyLength"/>, and the account
    /// belongs to one of the current user's connectors for that provider. Null when invalid.
    /// </summary>
    internal async Task<CalendarEventReference?> ValidateEventReferenceAsync(
        CalendarConnectorStore store,
        string? providerKey,
        string? accountKey,
        string? externalEventId,
        CancellationToken ct)
    {
        if (!CalendarEventReference.TryCreate(providerKey, accountKey, externalEventId, out var reference) || reference is null)
        {
            return null;
        }

        var connectors = await store.GetOwnByProviderAsync(reference.CalendarProvider, ct);
        var ownsAccount = connectors.Any(c => string.Equals(c.AccountKey, reference.AccountKey, StringComparison.OrdinalIgnoreCase));
        return ownsAccount ? reference : null;
    }

    /// <summary>
    /// Id of the existing time entry referencing the event triple (unique index ⇒ at most one), or null.
    /// </summary>
    public async Task<Guid?> FindTrackedEntryIdAsync(
        ApskaitaDbContext db,
        CalendarEventReference reference,
        CancellationToken ct)
    {
        return await db.TimeTrackings
            .Where(t => t.CalendarEvent != null
                && t.CalendarEvent!.CalendarProvider == reference.CalendarProvider
                && t.CalendarEvent.AccountKey == reference.AccountKey
                && t.CalendarEvent.ExternalEventId == reference.ExternalEventId)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Resolves the displayed week bounds (Monday..Sunday) from a raw <c>weekStart</c> query value without
    /// touching the database, so the calendar reader can be called before building the page model.
    /// </summary>
    public (DateOnly WeekStart, DateOnly WeekEnd) CreateWeekBounds(string? weekStartRaw)
    {
        var weekStart = GetWeekStart(ResolveAnchorDate(weekStartRaw));
        return (weekStart, weekStart.AddDays(6));
    }

    private static DateOnly ResolveAnchorDate(string? weekStartRaw)
    {
        if (DateOnly.TryParse(weekStartRaw, out var parsed))
        {
            return parsed;
        }

        return DateOnly.FromDateTime(DateTime.Now);
    }

    private static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var mondayOffset = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.AddDays(mondayOffset);
    }

    private static TimeTrackingEntryViewModel CreateEntryViewModel(TimeTrackingEntryRow row, TimeSpan displayOffset)
    {
        // Stored UTC → display-zone wall time; all grid math below works in wall time.
        var startedAtLocal = BrowserTimeZone.UtcToWall(row.StartedAtUtc, displayOffset);
        var durationMinutes = Math.Clamp(row.DurationInMinutes, MinimumDurationMinutes, TimeTrackingLimits.MaxDurationMinutes);
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
