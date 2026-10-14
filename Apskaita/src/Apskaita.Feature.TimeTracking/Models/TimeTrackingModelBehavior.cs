namespace Apskaita.Feature.TimeTracking.Models;

/// <summary>
/// Pure request behavior for the time-tracking dialog model (the dissolved content service's
/// non-data half): dialog-model factories, normalization, recurrence generation, and the saved
/// message. No database access lives here — reads go through <c>IQuery&lt;T&gt;</c> and writes
/// through <c>ITimeTrackingStore</c> in <c>TimeTrackingEndpoints</c>.
/// </summary>
public sealed partial class TimeTrackingModel
{
    private const int DefaultDurationMinutes = TimeTrackingLimits.DefaultDurationMinutes;
    private const int MinimumDurationMinutes = TimeTrackingLimits.MinDurationMinutes;
    private const int MaxOccurrences = TimeTrackingLimits.MaxOccurrences;
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
    private static readonly TimeOnly DefaultSlotStart = new(8, 0);

    /// <summary>Blank create-dialog model for the given raw query values (date/time default to the anchor week's Monday at 08:00).</summary>
    public static TimeTrackingModel CreateDefault(string? dateRaw, string? timeRaw, string? weekStartRaw)
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

    /// <summary>
    /// Rebuild of a submitted request for redisplay with validation errors: normalizes defaults
    /// (date/time/duration) and trims optional text, carrying the recurrence and event-reference fields.
    /// </summary>
    public static TimeTrackingModel FromRequest(
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
    public static TimeTrackingModel CreateFromEvent(
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

    /// <summary>Success toast message for a single logged entry.</summary>
    public string CreateSavedMessage(string projectLabel)
    {
        var duration = DurationMinutes;
        if (duration < MinimumDurationMinutes)
        {
            duration = DefaultDurationMinutes;
        }

        var description = string.IsNullOrWhiteSpace(Description)
            ? "no description"
            : Description.Trim();

        return $"Logged {duration} min for '{projectLabel}' ({description}).";
    }

    /// <summary>Success toast message; recurring batches report the created count.</summary>
    public string CreateSavedMessage(string projectLabel, int count)
    {
        if (count <= 1)
        {
            return CreateSavedMessage(projectLabel);
        }

        var description = string.IsNullOrWhiteSpace(Description)
            ? "no description"
            : Description.Trim();

        return $"Created {count} recurring entries for '{projectLabel}' ({description}).";
    }

    /// <summary>
    /// Display-zone wall start times for the request's occurrences: the base entry plus weekly
    /// steps until <c>RepeatUntil</c> (capped at <see cref="TimeTrackingLimits.MaxOccurrences"/>).
    /// </summary>
    public IReadOnlyList<DateTime> CreateOccurrences()
    {
        var baseStarted = ResolveStartedAtLocal();
        if (!Repeat || !RepeatUntil.HasValue)
        {
            return [baseStarted];
        }

        var stepDays = Math.Max(TimeTrackingLimits.MinRepeatWeeks, RepeatEveryWeeks ?? TimeTrackingLimits.MinRepeatWeeks) * TimeTrackingLimits.DaysPerWeek;
        var baseTime = TimeOnly.FromDateTime(baseStarted);
        var baseDate = DateOnly.FromDateTime(baseStarted);
        var results = new List<DateTime> { baseStarted };

        for (var i = 1; i < MaxOccurrences; i++)
        {
            var nextDate = baseDate.AddDays(i * stepDays);
            if (RepeatUntil.HasValue && nextDate > RepeatUntil.Value)
            {
                break;
            }

            results.Add(nextDate.ToDateTime(baseTime));
        }

        return results;
    }

    /// <summary>Resolves the dialog's date+start into a display-zone wall time; callers convert it to UTC before storing.</summary>
    public DateTime ResolveStartedAtLocal()
    {
        var date = Date == default ? DateOnly.FromDateTime(DateTime.Now) : Date;
        var start = Start == default ? DefaultSlotStart : Start;

        return date.ToDateTime(start);
    }

    /// <summary>Resolves the displayed week bounds (Monday..Sunday) from a raw <c>weekStart</c> query value.</summary>
    public static (DateOnly WeekStart, DateOnly WeekEnd) CreateWeekBounds(string? weekStartRaw)
    {
        var weekStart = GetWeekStart(ResolveAnchorDate(weekStartRaw));
        return (weekStart, weekStart.AddDays(6));
    }

    /// <summary>Parses a raw <c>weekStart</c> query value, falling back to today.</summary>
    public static DateOnly ResolveAnchorDate(string? weekStartRaw)
    {
        if (DateOnly.TryParse(weekStartRaw, out var parsed))
        {
            return parsed;
        }

        return DateOnly.FromDateTime(DateTime.Now);
    }

    /// <summary>Monday of the week containing <paramref name="date"/>.</summary>
    public static DateOnly GetWeekStart(DateOnly date)
    {
        var dayOfWeek = (int)date.DayOfWeek;
        var mondayOffset = dayOfWeek == 0 ? -6 : 1 - dayOfWeek;
        return date.AddDays(mondayOffset);
    }
}
