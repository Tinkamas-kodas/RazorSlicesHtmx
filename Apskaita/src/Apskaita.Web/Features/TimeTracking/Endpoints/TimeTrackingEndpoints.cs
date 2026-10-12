using Apskaita.Entities;
using Apskaita.Web.Features.TimeTracking.Services;
using Apskaita.Web.Features.TimeTracking.Slices;
using Apskaita.Web.Shared;
using Apskaita.Web.Shared.Calendar;
using Apskaita.Web.Shared.Routing;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TimeTrackingModel = Apskaita.Web.Features.TimeTracking.Models.TimeTrackingModel;

namespace Apskaita.Web.Features.TimeTracking.Endpoints;

public sealed class TimeTrackingEndpoints(FeatureResultBuilder resultBuilder) : BaseFeatureModule(resultBuilder)
{
    private readonly TimeTrackingContentService _content = new();

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

        group.MapGet("list", async (HttpContext ctx, string? weekStart, ApskaitaDbContext db, ICalendarEventReader calendarReader, CancellationToken ct) =>
        {
            // Calendar events are display-only enrichment (REQ-004/REQ-008); the reader never throws.
            var (weekStartDay, weekEndDay) = _content.CreateWeekBounds(weekStart);
            var calendar = await calendarReader.ReadAsync(weekStartDay, weekEndDay, ct);
            var model = await _content.CreatePageModelAsync(db, weekStart, calendar, ct);

            if (ctx.IsHtmxTarget("time-tracking-grid"))
            {
                return Result.Fragment(_Grid.Create(model));
            }

            return Result.Page(_ListPage.Create(model));
        });

        group.MapGet("create", (string? date, string? time, string? weekStart) =>
        {
            var model = _content.CreateCreateDialogModel(date, time, weekStart);
            return Result.ShowDialog(_CreateDialog.Create(model));
        });

        group.MapPost("create", async (
            [FromForm] TimeTrackingModel request,
            [FromServices] IValidator<TimeTrackingModel> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.ToErrorDictionary();
                var invalidModel = _content.CreateCreateDialogModel(request, errors);
                return Result.Fragment(_CreateDialog.Create(invalidModel));
            }

            var projectData = await db.Projects
                .AsNoTracking()
                .Where(project => project.Id == request.ProjectId)
                .Select(project => new
                {
                    Label = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
                })
                .FirstOrDefaultAsync(ct);

            if (projectData is null)
            {
                var invalidModel = _content.CreateCreateDialogModel(request,
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
            var occurrences = _content.CreateOccurrences(request);
            var rows = occurrences
                .Select(startedAtLocal => new Apskaita.Entities.TimeTracking
                {
                    Id = Guid.NewGuid(),
                    ProjectId = request.ProjectId!.Value,
                    StartedAtLocal = startedAtLocal,
                    DurationInMinutes = request.DurationMinutes,
                    HourlyRate = request.HourlyRate,
                    Description = request.Description!.Trim(),
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                    CreatedByUserId = Guid.Empty,
                    UpdatedByUserId = Guid.Empty
                })
                .ToArray();

            db.TimeTrackings.AddRange(rows);
            await db.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .ClearDialog()
                .WithTrigger("time-tracking-list-refresh")
                .WithToast(_content.CreateSavedMessage(request, projectData.Label, rows.Length), title: "Time entry", tone: ToastTone.Success);
        }).DisableAntiforgery();

        group.MapGet("edit/{id:guid}", async (Guid id, HttpContext ctx, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var model = await _content.CreateEditDialogModelAsync(db, id, ct);
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
            [FromForm] TimeTrackingModel request,
            [FromServices] IValidator<TimeTrackingModel> validator,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var row = await db.TimeTrackings.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            var validationResult = await validator.ValidateAsync(request, ct);
            if (!validationResult.IsValid)
            {
                var errors = validationResult.ToErrorDictionary();
                var invalidModel = _content.CreateCreateDialogModel(request, errors);
                return Result.Fragment(_EditDialog.Create(invalidModel));
            }

            var projectData = await db.Projects
                .AsNoTracking()
                .Where(project => project.Id == request.ProjectId)
                .Select(project => new
                {
                    Label = project.Code == null ? project.Name : $"{project.Name} ({project.Code})"
                })
                .FirstOrDefaultAsync(ct);

            if (projectData is null)
            {
                var invalidModel = _content.CreateCreateDialogModel(request,
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
            row.StartedAtLocal = _content.ResolveStartedAtLocal(request);
            row.DurationInMinutes = request.DurationMinutes;
            row.HourlyRate = request.HourlyRate;
            row.Description = request.Description!.Trim();
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;
            await db.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .ClearDialog()
                .WithTrigger("time-tracking-list-refresh")
                .WithToast($"Updated time entry for '{projectData.Label}'.", title: "Time entry", tone: ToastTone.Success);
        }).DisableAntiforgery();

        group.MapGet("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var model = await _content.CreateEditDialogModelAsync(db, id, ct);
            return model is null
                ? Results.NotFound()
                : Result.Fragment(_DeleteDialog.Create(model));
        });

        group.MapPost("delete/{id:guid}", async (Guid id, ApskaitaDbContext db, CancellationToken ct) =>
        {
            var row = await db.TimeTrackings.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            db.TimeTrackings.Remove(row);
            await db.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .ClearDialog()
                .WithTrigger("time-tracking-list-refresh")
                .WithToast("Time entry was deleted.", title: "Time entry", tone: ToastTone.Warning);
        }).DisableAntiforgery();

        group.MapPost("move/{id:guid}", async (
            Guid id,
            string date,
            string time,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var row = await db.TimeTrackings.FindAsync([id], ct);
            if (row is null)
            {
                return Results.NotFound();
            }

            if (!DateOnly.TryParse(date, out var newDate) || !TimeOnly.TryParse(time, out var newTime))
            {
                return Results.BadRequest();
            }

            row.StartedAtLocal = newDate.ToDateTime(newTime);
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedByUserId = Guid.Empty;
            await db.SaveChangesAsync(ct);

            return Result.Fragment(_Empty.Create())
                .WithTrigger("time-tracking-list-refresh")
                .WithToast("Moved time entry.", title: "Time entry", tone: ToastTone.Success);
        }).DisableAntiforgery();

        group.MapGet("project-lookup", async (
            string? query,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            var model = await _content.CreateProjectLookupModelAsync(db, query, ct);
            return Result.Fragment(_ProjectLookupItems.Create(model));
        });

        group.MapGet("project-lookup-selected", async (
            string value,
            ApskaitaDbContext db,
            CancellationToken ct) =>
        {
            if (!Guid.TryParse(value, out var projectId))
            {
                return Results.NotFound();
            }

            var selected = await db.Projects
                .AsNoTracking()
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
}
