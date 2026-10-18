using Apskaita.Entities.Contracts;
using Apskaita.Feature.WorkActs.Models;
using Apskaita.Feature.WorkActs.Persistence;
using Apskaita.Feature.WorkActs.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Routing;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.WorkActs.Endpoints;

public sealed partial class WorkActEndpoints
{
    /// <summary>
    /// Creation flow: the project+period dialog (<c>create</c>) and its post. A valid post snapshots the
    /// period's entries into a new Draft act with its lines in one save and navigates to the review page
    /// (<c>GET review/{id}</c>); an invalid one re-renders the dialog with inline field errors. The act's
    /// lines are copies: creating or editing an act never changes the time entries behind them.
    /// </summary>
    private sealed class CreateSection(WorkActEndpoints owner, ILogger<WorkActEndpoints> logger)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("create", async (IQuery<Project> projects, CancellationToken ct) =>
            {
                var options = await LoadProjectOptionsAsync(projects, ct);
                return owner.Result.ShowDialog(_CreateDialog.Create(new WorkActCreateDialogModel(options, WorkActCreateRequest.CreateDefault())));
            });

            // The dialog posts here. Field validation is entirely server-side (FluentValidation); the
            // period's entries become the act's lines, so nothing is created when the period is empty.
            group.MapPost("create", async (
                HttpContext ctx,
                IFeatureUri uri,
                [FromForm] WorkActCreateRequest request,
                IValidator<WorkActCreateRequest> validator,
                IQuery<Project> projects,
                IQuery<TimeTracking> entries,
                IWorkActStore store,
                CancellationToken ct) =>
            {
                var validationResult = await validator.ValidateAsync(request, ct);
                if (!validationResult.IsValid)
                {
                    var options = await LoadProjectOptionsAsync(projects, ct);
                    var invalid = WorkActCreateRequest.FromRequest(request, validationResult.ToErrorDictionary());
                    return owner.Result.Fragment(_CreateDialog.Create(new WorkActCreateDialogModel(options, invalid)));
                }

                var project = await projects.All().FirstOrDefaultAsync(p => p.Code == request.ProjectCode, ct);
                if (project is null)
                {
                    // The validator guarantees the project exists; this guards the race between them.
                    return owner.Result.Fragment(_Empty.Create())
                        .WithRetarget("#dialog-host")
                        .WithToast("Project not found.", title: "Work act", tone: ToastTone.Warning);
                }

                var offset = BrowserTimeZone.GetDisplayOffset(ctx);
                var (fromUtc, toUtc) = ToUtcBounds(request.DateFrom, request.DateTo, offset);

                var covered = await entries.All()
                    .Where(t => t.ProjectId == project.Id && t.StartedAtUtc >= fromUtc && t.StartedAtUtc < toUtc)
                    .OrderBy(t => t.StartedAtUtc)
                    .Select(t => new { t.Id, t.StartedAtUtc, t.DurationInMinutes, t.Description, t.HourlyRate })
                    .ToListAsync(ct);

                if (covered.Count == 0)
                {
                    return owner.Result.Fragment(_Empty.Create())
                        .WithRetarget("#dialog-host")
                        .WithToast("There are no time entries in this period, so there is nothing to certify.", title: "Work act", tone: ToastTone.Warning);
                }

                var actId = Guid.NewGuid();
                var now = DateTime.UtcNow;
                var lineRows = covered
                    .Select(e => new WorkActLine
                    {
                        Id = Guid.NewGuid(),
                        WorkActId = actId,
                        TimeTrackingId = e.Id,
                        DurationMinutes = e.DurationInMinutes,
                        HourlyRate = e.HourlyRate ?? project.HourlyRate,
                        StartedAtUtc = e.StartedAtUtc,
                        Description = e.Description?.Trim() ?? string.Empty,
                        CreatedAtUtc = now,
                        CreatedByUserId = Guid.Empty,
                        UpdatedAtUtc = now,
                        UpdatedByUserId = Guid.Empty
                    })
                    .ToArray();

                var totalHours = Math.Round(lineRows.Sum(l => l.DurationMinutes) / 60m, 2, MidpointRounding.AwayFromZero);
                var totalAmount = Math.Round(lineRows.Sum(l => AmountOf(l.DurationMinutes, l.HourlyRate)), 2, MidpointRounding.AwayFromZero);

                store.WorkActs.Add(new WorkAct
                {
                    Id = actId,
                    ProjectId = project.Id,
                    ClientId = project.ClientId,
                    CurrencyCode = project.CurrencyCode,
                    PeriodStartUtc = fromUtc,
                    PeriodEndUtc = toUtc,
                    TotalHours = totalHours,
                    TotalAmount = totalAmount,
                    Status = WorkActStatus.Draft,
                    CreatedAtUtc = now,
                    CreatedByUserId = Guid.Empty,
                    UpdatedAtUtc = now,
                    UpdatedByUserId = Guid.Empty,
                    Version = 0
                });
                store.WorkActLines.AddRange(lineRows);

                try
                {
                    // One save flushes the act row and every line together.
                    await store.SaveChangesAsync(ct);
                }
                catch (DbUpdateException ex)
                {
                    logger.LogWarning(ex, "work act creation failed for project {ProjectId}", project.Id);
                    return owner.Result.Fragment(_Empty.Create())
                        .WithRetarget("#dialog-host")
                        .WithToast("The work act could not be created; nothing was stored.", title: "Work act", tone: ToastTone.Error);
                }

                // Success: open the review page of the new act; its response clears the dialog.
                return owner.Result.Fragment(_Empty.Create())
                    .WithLocation(uri.Uri($"review/{actId}"))
                    .WithToast($"Work act created with {lineRows.Length} lines ({totalHours:0.##} h).", title: "Work act", tone: ToastTone.Success);
            });

            // The review page: the act's lines, addressed by id, no query validation — it just displays
            // the work items (the same screen the list's Edit action opens for a Draft act).
            group.MapGet("review/{id:guid}", async (
                Guid id,
                HttpContext ctx,
                IQuery<WorkAct> acts,
                IQuery<Project> projects,
                IQuery<WorkActLine> lines,
                CancellationToken ct) =>
            {
                var act = await acts.All().FirstOrDefaultAsync(a => a.Id == id, ct);
                if (act is null)
                {
                    return Results.NotFound();
                }

                var model = await CreateEditModelAsync(ctx, act, acts, projects, lines, ct);
                return owner.Result.Page(_Edit.Create(model))
                    .ClearDialog()
                    .WithPageTitle("Review work act");
            });
        }

        private static async Task<IReadOnlyList<WorkActProjectOption>> LoadProjectOptionsAsync(IQuery<Project> projects, CancellationToken ct)
        {
            var rows = await projects.All()
                .OrderBy(p => p.Name)
                .Select(p => new { p.Id, p.Code, Label = string.IsNullOrWhiteSpace(p.Code) ? p.Name : $"{p.Name} ({p.Code})" })
                .ToListAsync(ct);

            return rows.Select(r => new WorkActProjectOption(r.Id, r.Code, r.Label, false)).ToArray();
        }
    }
}
