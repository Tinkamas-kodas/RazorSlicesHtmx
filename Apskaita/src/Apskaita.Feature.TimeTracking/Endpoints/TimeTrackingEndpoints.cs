using System.Globalization;
using Apskaita.Entities;
using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Models;
using Apskaita.Feature.TimeTracking.Persistence;
using Apskaita.Feature.TimeTracking.Slices;
using Apskaita.Shared.Calendar;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.Calendar;
using Apskaita.Web.Shared.ProjectColors;
using Apskaita.Web.Shared.Routing;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrackingEntry = Apskaita.Entities.TimeTracking;
using TimeTrackingModel = Apskaita.Feature.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Feature.TimeTracking.Endpoints;

/// <summary>
/// Time-tracking feature endpoints. Reads compose over <see cref="IQuery{T}"/> (no-tracking);
/// writes mutate through <see cref="ITimeTrackingStore"/> (tracked). Calendar event references are
/// validated against Account's connectors via <see cref="ICalendarEventReferenceResolver"/> — the
/// feature never touches another feature's tables directly.
/// </summary>
public sealed class TimeTrackingEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private const int SlotMinutes = 30;
    private const double SlotHeightPx = 34d;
    private static readonly TimeOnly WorkDayStart = new(8, 0);
    private static readonly TimeOnly WorkDayEnd = new(17, 0);

    public override IReadOnlyList<AdminNavItem> NavigationItems =>
    [
        new("MAIN", "time-tracking", "Time tracking", "list", Order: 1, AuthorizationPolicy: AuthPolicies.Operational)
    ];

    public override void MapEndpoints(RouteGroupBuilder group)
    {
        group.RequireAuthorization(AuthPolicies.Operational);

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
            // Calendar events are display-only enrichment (REQ-004/REQ-008); the reader never throws.
            var (weekStartDay, weekEndDay) = TimeTrackingModel.CreateWeekBounds(weekStart);
            var displayOffset = BrowserTimeZone.GetDisplayOffset(ctx);
            var calendar = await calendarReader.ReadAsync(weekStartDay, weekEndDay, ct);
            var model = await CreatePageModelAsync(entries, projects, weekStart, calendar, displayOffset, ct);

            if (ctx.IsHtmxTarget("time-tracking-grid"))
            {
                return Result.Fragment(_Grid.Create(model));
            }

            return Result.Page(_ListPage.Create(model));
        });

        group.MapGet("create", (string? date, string? time, string? weekStart) =>
        {
            var model = TimeTrackingModel.CreateDefault(date, time, weekStart);
            return Result.ShowDialog(_CreateDialog.Create(model));
        });

        // Single URI for every calendar-event click (REQ-001): the server decides which dialog to
        // return — the existing entry's edit dialog when an entry references the event, otherwise a
        // create dialog prefilled with the event's display-zone start and duration.
        group.MapGet("event", async (
            HttpContext ctx,
            IFeatureUri uri,
            string provider,
            string account,
            string eventId,
            string? date,
            string? time,
            int? duration,
            string? weekStart,
            IQuery<TimeTrackingEntry> entries,
            ICalendarEventReferenceResolver eventResolver,
            CancellationToken ct) =>
        {
            var reference = await ValidateEventReferenceAsync(eventResolver, provider, account, eventId, ct);
            if (reference is null)
            {
                return Results.NotFound();
            }

            var trackedId = await FindTrackedEntryIdAsync(entries, reference, ct);
            if (trackedId is not null)
            {
                // Reuse the edit route (route id is authoritative for the dialog's targets).
                return Results.Redirect(uri.Uri($"edit/{trackedId.Value}"));
            }

            var model = TimeTrackingModel.CreateFromEvent(date, time, duration, weekStart, reference.ProviderKey, reference.AccountKey, reference.ExternalEventId);
            return Result.ShowDialog(_CreateDialog.Create(model));
        });

        group.MapPost("create", async (
            HttpContext ctx,
            [FromForm] TimeTrackingModel request,
            [FromServices] IValidator<TimeTrackingModel> validator,
            ITimeTrackingStore store,
            IQuery<Project> projects,
            IQuery<TimeTrackingEntry> entries,
            ICalendarEventReferenceResolver eventResolver,
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.ToErrorDictionary();
                var invalidModel = TimeTrackingModel.FromRequest(request, errors);
                return Result.Fragment(_CreateDialog.Create(invalidModel));
            }

            var projectData = await projects.All()
                .Where(project => project.Id == request.ProjectId)
                .Select(project => new
                {
                    Label = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
                })
                .FirstOrDefaultAsync(ct);

            if (projectData is null)
            {
                var invalidModel = TimeTrackingModel.FromRequest(request,
                    new Dictionary<string, (string errorCode, string errorMessage)[]>
                    {
                        [nameof(TimeTrackingModel.ProjectId)] =
                        [
                            ("not_found", "Selected project was not found.")
                        ]
                    });
                return Result.Fragment(_CreateDialog.Create(invalidModel));
            }

            var now = DateTime.UtcNow;
            var displayOffset = BrowserTimeZone.GetDisplayOffset(ctx);

            // Calendar event reference (all-or-nothing); invalid or forged reference → dialog error.
            var hasAnyEventField = !string.IsNullOrEmpty(request.EventProviderKey)
                || !string.IsNullOrEmpty(request.EventAccountKey)
                || !string.IsNullOrEmpty(request.EventId);
            CalendarEventReference? calendarEvent = null;
            if (hasAnyEventField)
            {
                calendarEvent = await ValidateEventReferenceAsync(eventResolver, request.EventProviderKey, request.EventAccountKey, request.EventId, ct);
                if (calendarEvent is null)
                {
                    var invalidModel = TimeTrackingModel.FromRequest(request,
                        new Dictionary<string, (string errorCode, string errorMessage)[]>
                        {
                            [nameof(TimeTrackingModel.EventId)] = [("invalid_event_reference", "The calendar event reference is invalid or its account is not connected.")]
                        });
                    return Result.Fragment(_CreateDialog.Create(invalidModel));
                }

                if (request.Repeat)
                {
                    var invalidModel = TimeTrackingModel.FromRequest(request,
                        new Dictionary<string, (string errorCode, string errorMessage)[]>
                        {
                            [nameof(TimeTrackingModel.Repeat)] = [("invalid_recurrence", "Recurring entries cannot reference a calendar event.")]
                        });
                    return Result.Fragment(_CreateDialog.Create(invalidModel));
                }

                // App-level dedupe (the unique index remains the race backstop): one entry per event.
                if (await FindTrackedEntryIdAsync(entries, calendarEvent, ct) is not null)
                {
                    return Result.Fragment(_Empty.Create())
                        .ClearDialog()
                        .WithTrigger("time-tracking-list-refresh")
                        .WithToast("This calendar event is already logged.", title: "Time entry", tone: ToastTone.Warning);
                }
            }

            var occurrences = request.CreateOccurrences();
            var rows = occurrences
                .Select(startedAtLocal => new TimeTrackingEntry
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId!.Value,
                    // Dialog times are display-zone wall time; the column stores UTC.
                    StartedAtUtc = BrowserTimeZone.WallToUtc(startedAtLocal, displayOffset),
                    DurationInMinutes = request.DurationMinutes,
                    HourlyRate = request.HourlyRate,
                    Description = request.Description!.Trim(),
                    // Fresh instance per row: an owned reference belongs to exactly one owner.
                    CalendarEvent = calendarEvent is null ? null : new CalendarEventReference(calendarEvent.CalendarProvider, calendarEvent.AccountKey, calendarEvent.ExternalEventId),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    CreatedByUserId = Guid.Empty,
                    UpdatedByUserId = Guid.Empty
                })
                .ToArray();

            store.TimeTrackings.AddRange(rows);
            try
            {
                await store.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Unique index race: the event was logged (here or in another tab) since the panel rendered.
                return Result.Fragment(_Empty.Create())
                    .ClearDialog()
                    .WithTrigger("time-tracking-list-refresh")
                    .WithToast("This calendar event is already logged.", title: "Time entry", tone: ToastTone.Warning);
            }

            return Result.Fragment(_Empty.Create())
                .ClearDialog()
                .WithTrigger("time-tracking-list-refresh")
                .WithToast(request.CreateSavedMessage(projectData.Label, rows.Length), title: "Time entry", tone: ToastTone.Success);
        });

        group.MapGet("edit/{id:guid}", async (Guid id, HttpContext ctx, IQuery<TimeTrackingEntry> entries, IQuery<Project> projects, CancellationToken ct) =>
        {
            var model = await CreateEditDialogModelAsync(entries, projects, id, BrowserTimeZone.GetDisplayOffset(ctx), ct);
            if (model is null)
            {
                return Results.NotFound();
            }

            return ctx.IsHtmxTarget("time-tracking-dialog")
                ? Result.Fragment(_EditDialog.Create(model))
                : Result.ShowDialog(_EditDialog.Create(model));
        });

        group.MapPost("edit/{id:guid}", async (
            Guid id,
            HttpContext ctx,
            [FromForm] TimeTrackingModel request,
            [FromServices] IValidator<TimeTrackingModel> validator,
            ITimeTrackingStore store,
            IQuery<Project> projects,
            CancellationToken ct) =>
        {
            var row = await store.TimeTrackings.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.ToErrorDictionary();
                var invalidModel = TimeTrackingModel.FromRequest(request, errors);
                return Result.Fragment(_EditDialog.Create(invalidModel));
            }

            var projectData = await projects.All()
                .Where(project => project.Id == request.ProjectId)
                .Select(project => new
                {
                    Label = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
                })
                .FirstOrDefaultAsync(ct);

            if (projectData is null)
            {
                var invalidModel = TimeTrackingModel.FromRequest(request,
                    new Dictionary<string, (string errorCode, string errorMessage)[]>
                    {
                        [nameof(TimeTrackingModel.ProjectId)] =
                        [
                            ("not_found", "Selected project was not found.")
                        ]
                    });
                return Result.Fragment(_EditDialog.Create(invalidModel));
            }

            row.ProjectId = request.ProjectId!.Value;
            row.StartedAtUtc = BrowserTimeZone.WallToUtc(request.ResolveStartedAtLocal(), BrowserTimeZone.GetDisplayOffset(ctx));
            row.DurationInMinutes = request.DurationMinutes;
            row.HourlyRate = request.HourlyRate;
            row.Description = request.Description!.Trim();
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;
            await store.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .ClearDialog()
                .WithTrigger("time-tracking-list-refresh")
                .WithToast($"Updated time entry for '{projectData.Label}'.", title: "Time entry", tone: ToastTone.Success);
        });

        group.MapGet("delete/{id:guid}", async (Guid id, HttpContext ctx, IQuery<TimeTrackingEntry> entries, IQuery<Project> projects, CancellationToken ct) =>
        {
            var model = await CreateEditDialogModelAsync(entries, projects, id, BrowserTimeZone.GetDisplayOffset(ctx), ct);
            return model is null
                ? Results.NotFound()
                : Result.Fragment(_DeleteDialog.Create(model));
        });

        group.MapPost("delete/{id:guid}", async (Guid id, ITimeTrackingStore store, CancellationToken ct) =>
        {
            var row = await store.TimeTrackings.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            store.TimeTrackings.Remove(row);
            await store.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .ClearDialog()
                .WithTrigger("time-tracking-list-refresh")
                .WithToast("Time entry was deleted.", title: "Time entry", tone: ToastTone.Warning);
        });

        group.MapPost("move/{id:guid}", async (
            Guid id,
            HttpContext ctx,
            string date,
            string time,
            ITimeTrackingStore store,
            CancellationToken ct) =>
        {
            var row = await store.TimeTrackings.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            if (!DateOnly.TryParse(date, out var newDate) || !TimeOnly.TryParse(time, out var newTime))
            {
                return Results.BadRequest();
            }

            row.StartedAtUtc = BrowserTimeZone.WallToUtc(newDate.ToDateTime(newTime), BrowserTimeZone.GetDisplayOffset(ctx));
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;
            await store.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .WithTrigger("time-tracking-list-refresh")
                .WithToast("Moved time entry.", title: "Time entry", tone: ToastTone.Success);
        });

        group.MapGet("project-lookup", async (
            string? query,
            IQuery<Project> projects,
            CancellationToken ct) =>
        {
            var model = await CreateProjectLookupModelAsync(projects, query, ct);
            return Result.Fragment(_ProjectLookupItems.Create(model));
        });

        group.MapGet("project-lookup-selected", async (
            string value,
            IQuery<Project> projects,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(value, out var projectId))
            {
                return Results.NotFound();
            }

            var selected = await projects.All()
                .Where(project => project.Id == projectId && project.IsActive)
                .Select(project => new
                {
                    value = project.Id.ToString(),
                    text = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
                })
                .FirstOrDefaultAsync(ct);

            return selected is null ? Results.NotFound() : Results.Ok(selected);
        });
    }

    // ---------------------------------------------------------------------------------------------
    // Page-model composition (reads over IQuery; no-tracking by contract)
    // ---------------------------------------------------------------------------------------------

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

    private static async Task<TimeTrackingModel?> CreateEditDialogModelAsync(
        IQuery<TimeTrackingEntry> entries,
        IQuery<Project> projects,
        Guid id,
        TimeSpan displayOffset,
        CancellationToken ct)
    {
        var row = await (
            from time in entries.All()
            join project in projects.All() on time.ProjectId equals project.Id
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
            Errors = new Dictionary<string, (string errorCode, string errorMessage)[]>()
        };
    }

    private static async Task<Dictionary<Guid, string?>> CreateProjectLookupModelAsync(
        IQuery<Project> projects,
        string? query,
        CancellationToken ct)
    {
        var term = query?.Trim();

        var active = projects.All()
            .Where(project => project.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            active = active.Where(project =>
                project.Name.Contains(term) ||
                (project.Code != null && project.Code.Contains(term)));
        }

        var items = await active
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

    /// <summary>
    /// Id of the existing time entry referencing the event triple (unique index ⇒ at most one), or null.
    /// </summary>
    private static async Task<Guid?> FindTrackedEntryIdAsync(
        IQuery<TimeTrackingEntry> entries,
        CalendarEventReference reference,
        CancellationToken ct)
    {
        return await entries.All()
            .Where(t => t.CalendarEvent != null
                && t.CalendarEvent!.CalendarProvider == reference.CalendarProvider
                && t.CalendarEvent.AccountKey == reference.AccountKey
                && t.CalendarEvent.ExternalEventId == reference.ExternalEventId)
            .Select(t => (Guid?)t.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Validates a calendar event reference triple (REQ-004): provider key parses case-insensitively,
    /// keys are non-blank and within <see cref="CalendarEventReference.MaxKeyLength"/>, and the account
    /// belongs to one of the current user's connectors for that provider (via Account's resolver).
    /// Null when invalid.
    /// </summary>
    private static async Task<CalendarEventReference?> ValidateEventReferenceAsync(
        ICalendarEventReferenceResolver eventResolver,
        string? providerKey,
        string? accountKey,
        string? externalEventId,
        CancellationToken ct)
    {
        if (!CalendarEventReference.TryCreate(providerKey, accountKey, externalEventId, out var reference) || reference is null)
        {
            return null;
        }

        return await eventResolver.OwnsAccountAsync(reference.CalendarProvider, reference.AccountKey, ct) ? reference : null;
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
