using System.Globalization;
using Apskaita.Entities;
using Apskaita.Web.Features.TimeTracking.Models;
using Apskaita.Web.Shared.ProjectColors;
using Microsoft.EntityFrameworkCore;
using TimeTrackingModel = Apskaita.Web.Features.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Web.Features.TimeTracking.Services;

public sealed class TimeTrackingContentService
{
    private const int DefaultDurationMinutes = 30;
    private const int MinimumDurationMinutes = 1;
    private const int SlotMinutes = 30;
    private const double SlotHeightPx = 34d;
    private const int MaxOccurrences = 104;
    private static readonly IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> EmptyErrors =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
    private static readonly TimeOnly WorkDayStart = new(8, 0);
    private static readonly TimeOnly WorkDayEnd = new(17, 0);
    private static readonly TimeOnly DefaultSlotStart = new(8, 0);

    public async Task<TimeTrackingPageModel> CreatePageModelAsync(ApskaitaDbContext db, string? weekStartRaw, CancellationToken ct)
    {
        var anchorDate = ResolveAnchorDate(weekStartRaw);
        var weekStart = GetWeekStart(anchorDate);
        var weekStartLocal = weekStart.ToDateTime(TimeOnly.MinValue);
        var weekEndExclusiveLocal = weekStartLocal.AddDays(7);

        var days = Enumerable
            .Range(0, 7)
            .Select(offset =>
            {
                var day = weekStart.AddDays(offset);
                return new TimeTrackingDayModel(
                    day,
                    day.ToString("ddd dd.MM", CultureInfo.InvariantCulture),
                    day == DateOnly.FromDateTime(DateTime.Now));
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
            where time.StartedAtLocal >= weekStartLocal && time.StartedAtLocal < weekEndExclusiveLocal
            orderby time.StartedAtLocal
            select new TimeTrackingEntryRow(
                time.Id,
                time.StartedAtLocal,
                time.DurationInMinutes,
                time.Description,
                project.ColorRgb)
            )
            .ToListAsync(ct);

        var entries = rows.Select(CreateEntryViewModel).ToArray();

        return new TimeTrackingPageModel(
            weekStart,
            weekStart.AddDays(6),
            weekStart.AddDays(-7),
            weekStart.AddDays(7),
            days,
            slots,
            entries);
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
            Errors = errors
        };
    }

    public async Task<TimeTrackingModel?> CreateEditDialogModelAsync(
        ApskaitaDbContext db,
        Guid id,
        CancellationToken ct)
    {
        var row = await (
            from time in db.TimeTrackings.AsNoTracking()
            join project in db.Projects.AsNoTracking() on time.ProjectId equals project.Id
            where time.Id == id
            select new
            {
                time.StartedAtLocal,
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

        return new TimeTrackingModel
        {
            Date = DateOnly.FromDateTime(row.StartedAtLocal),
            Start = TimeOnly.FromDateTime(row.StartedAtLocal),
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
        if (!request.Repeat)
        {
            return [baseStarted];
        }

        var stepDays = Math.Max(1, request.RepeatEveryWeeks ?? 1) * 7;
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

    public DateTime ResolveStartedAtLocal(TimeTrackingModel request)
    {
        var date = request.Date == default ? DateOnly.FromDateTime(DateTime.Now) : request.Date;
        var start = request.Start == default ? DefaultSlotStart : request.Start;

        return date.ToDateTime(start);
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

    private static TimeTrackingEntryViewModel CreateEntryViewModel(TimeTrackingEntryRow row)
    {
        var startedAtLocal = row.StartedAtLocal;
        var durationMinutes = Math.Clamp(row.DurationInMinutes, MinimumDurationMinutes, 1440);
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
        DateTime StartedAtLocal,
        int DurationInMinutes,
        string Description,
        string ColorRgb);
}
