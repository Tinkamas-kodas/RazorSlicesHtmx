using Apskaita.Entities.Contracts;
using Apskaita.Feature.TimeTracking.Persistence;
using Apskaita.Feature.TimeTracking.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Calendar;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrackingEntry = Apskaita.Entities.TimeTracking;
using TimeTrackingModel = Apskaita.Feature.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Feature.TimeTracking.Endpoints;

public sealed partial class TimeTrackingEndpoints
{
    /// <summary>
    /// Entry lifecycle: create (with weekly recurrence batch), edit, delete and drag-move. Writes go
    /// through <see cref="ITimeTrackingStore"/> (tracked); the calendar event reference is validated
    /// against Account's connectors before anything is persisted.
    /// </summary>
    private sealed class EntrySection(TimeTrackingEndpoints owner, ILogger<TimeTrackingEndpoints> logger)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("create", (string? date, string? time, string? weekStart) =>
            {
                var model = TimeTrackingModel.CreateDefault(date, time, weekStart);
                return owner.Result.ShowDialog(_CreateDialog.Create(model));
            });

            group.MapPost("create", async (
                HttpContext ctx,
                [FromForm] TimeTrackingModel request,
                [FromServices] IValidator<TimeTrackingModel> validator,
                ITimeTrackingStore store,
                IQuery<TimeTrackingEntry> entries,
                ICalendarEventReferenceResolver eventResolver,
                CancellationToken ct) =>
            {
                var validationResult = await validator.ValidateAsync(request, ct);
                if (!validationResult.IsValid)
                {
                    var errors = validationResult.ToErrorDictionary();
                    var invalidModel = TimeTrackingModel.FromRequest(request, errors);
                    return owner.Result.Fragment(_CreateDialog.Create(invalidModel));
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
                    calendarEvent = await ValidateEventReferenceAsync(
                        eventResolver, request.EventProviderKey, request.EventAccountKey, request.EventId, ct);
                    if (calendarEvent is null)
                    {
                        var invalidModel = TimeTrackingModel.FromRequest(request,
                            new Dictionary<string, (string errorCode, string errorMessage)[]>
                            {
                                [nameof(TimeTrackingModel.EventId)] = [("invalid_event_reference", "The calendar event reference is invalid or its account is not connected.")]
                            });
                        return owner.Result.Fragment(_CreateDialog.Create(invalidModel));
                    }

                    if (request.Repeat)
                    {
                        var invalidModel = TimeTrackingModel.FromRequest(request,
                            new Dictionary<string, (string errorCode, string errorMessage)[]>
                            {
                                [nameof(TimeTrackingModel.Repeat)] = [("invalid_recurrence", "Recurring entries cannot reference a calendar event.")]
                            });
                        return owner.Result.Fragment(_CreateDialog.Create(invalidModel));
                    }

                    // App-level dedupe (the unique index remains the race backstop): one entry per event.
                    if (await FindTrackedEntryIdAsync(entries, calendarEvent, ct) is not null)
                    {
                        return owner.Result.Fragment(_Empty.Create())
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
                    logger.LogWarning("Unique index race: the calendar event was already logged.");
                    // Unique index race: the event was logged (here or in another tab) since the panel rendered.
                    return owner.Result.Fragment(_Empty.Create())
                        .ClearDialog()
                        .WithTrigger("time-tracking-list-refresh")
                        .WithToast("This calendar event is already logged.", title: "Time entry", tone: ToastTone.Warning);
                }

                return owner.Result.Fragment(_Empty.Create())
                    .ClearDialog()
                    .WithTrigger("time-tracking-list-refresh")
                    .WithToast(request.CreateSavedMessage(request.ProjectLabel ?? "", rows.Length), title: "Time entry", tone: ToastTone.Success);
            });

            group.MapGet("edit/{id:guid}", async (Guid id, HttpContext ctx, IQuery<TimeTrackingEntry> entries, IQuery<Project> projects, CancellationToken ct) =>
            {
                var model = await CreateEditDialogModelAsync(entries, projects, id, BrowserTimeZone.GetDisplayOffset(ctx), ct);
                if (model is null)
                {
                    return Results.NotFound();
                }

                return ctx.IsHtmxTarget("time-tracking-dialog")
                    ? owner.Result.Fragment(_EditDialog.Create(model))
                    : owner.Result.ShowDialog(_EditDialog.Create(model));
            });

            group.MapPost("edit/{id:guid}", async (
                Guid id,
                HttpContext ctx,
                [FromForm] TimeTrackingModel request,
                [FromServices] IValidator<TimeTrackingModel> validator,
                ITimeTrackingStore store,
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
                    return owner.Result.Fragment(_EditDialog.Create(invalidModel));
                }

                row.ProjectId = request.ProjectId!.Value;
                row.StartedAtUtc = BrowserTimeZone.WallToUtc(request.ResolveStartedAtLocal(), BrowserTimeZone.GetDisplayOffset(ctx));
                row.DurationInMinutes = request.DurationMinutes;
                row.HourlyRate = request.HourlyRate;
                row.Description = request.Description!.Trim();
                row.UpdatedAtUtc = DateTime.UtcNow;
                row.UpdatedByUserId = Guid.Empty;
                await store.SaveChangesAsync(ct);

                return owner.Result.Fragment(_Empty.Create())
                    .ClearDialog()
                    .WithTrigger("time-tracking-list-refresh")
                    .WithToast($"Updated time entry for '{request.ProjectLabel}'.", title: "Time entry", tone: ToastTone.Success);
            });

            group.MapGet("delete/{id:guid}", async (Guid id, HttpContext ctx, IQuery<TimeTrackingEntry> entries, IQuery<Project> projects, CancellationToken ct) =>
            {
                var model = await CreateEditDialogModelAsync(entries, projects, id, BrowserTimeZone.GetDisplayOffset(ctx), ct);
                return model is null
                    ? Results.NotFound()
                    : owner.Result.Fragment(_DeleteDialog.Create(model));
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

                return owner.Result.Fragment(_Empty.Create())
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

                return owner.Result.Fragment(_Empty.Create())
                    .WithTrigger("time-tracking-list-refresh")
                    .WithToast("Moved time entry.", title: "Time entry", tone: ToastTone.Success);
            });
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
    }
}
