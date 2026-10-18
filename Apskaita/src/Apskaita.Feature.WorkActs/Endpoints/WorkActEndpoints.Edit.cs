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
    /// Draft act editing: the edit screen (<c>edit/{id}</c>), the persisted per-line saves
    /// (<c>edit/line</c>) and the Final action (<c>final/{id}</c>). Line edits land on
    /// <c>work_act_lines</c> rows and recompute the stored totals; the referenced time entries are never
    /// modified here. A Final (or invoiced) act renders its read-only detail instead.
    /// </summary>
    private sealed class EditSection(WorkActEndpoints owner, ILogger<WorkActEndpoints> logger)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("edit/{id:guid}", async (
                Guid id,
                HttpContext ctx,
                IQuery<WorkAct> acts,
                IQuery<Project> projects,
                IQuery<WorkActLine> lines,
                IQuery<InvoiceLine> invoiceLines,
                CancellationToken ct) =>
            {
                var act = await acts.All().FirstOrDefaultAsync(a => a.Id == id, ct);
                if (act is null)
                {
                    return Results.NotFound();
                }

                if (act.Status != WorkActStatus.Draft)
                {
                    var frozen = await CreateDetailModelAsync(ctx, act, acts, projects, lines, invoiceLines, ct);
                    return ctx.IsHtmxTarget("work-acts-detail")
                        ? owner.Result.Fragment(_Detail.Create(frozen))
                        : owner.Result.Page(_Detail.Create(frozen));
                }

                var model = await CreateEditModelAsync(ctx, act, acts, projects, lines, ct);
                return ctx.IsHtmxTarget("work-acts-detail")
                    ? owner.Result.Fragment(_Edit.Create(model))
                    : owner.Result.Page(_Edit.Create(model));
            });

            // One line saved: the row is updated, the totals recomputed from all the act's lines, and only
            // this row plus the totals line come back.
            group.MapPost("edit/line", async (
                HttpContext ctx,
                [FromForm] WorkActLineEditRequest request,
                [FromServices] IValidator<WorkActLineEditRequest> validator,
                IQuery<WorkAct> acts,
                IQuery<Project> projects,
                IQuery<WorkActLine> lines,
                IWorkActStore store,
                CancellationToken ct) =>
            {
                var act = await store.WorkActs.FirstOrDefaultAsync(a => a.Id == request.ActId, ct);
                if (act is null)
                {
                    return Results.NotFound();
                }

                if (act.Status != WorkActStatus.Draft)
                {
                    return owner.Result.Fragment(_Empty.Create())
                        .WithToast("This work act is final and its lines can no longer be changed.", title: "Work act", tone: ToastTone.Warning)
                        .WithTrigger("work-acts-list-refresh");
                }

                var line = await store.WorkActLines.FirstOrDefaultAsync(l => l.Id == request.LineId && l.WorkActId == act.Id, ct);
                if (line is null)
                {
                    return Results.NotFound();
                }

                var validationResult = await validator.ValidateAsync(request, ct);
                var error = validationResult.IsValid
                    ? null
                    : string.Join(" ", validationResult.Errors.Select(e => e.ErrorMessage).Distinct());

                if (error is null)
                {
                    line.DurationMinutes = request.DurationMinutes;
                    line.Description = request.Description!.Trim();
                    line.HourlyRate = request.HourlyRate;
                    line.UpdatedAtUtc = DateTime.UtcNow;
                    line.UpdatedByUserId = Guid.Empty;

                    // Totals are recomputed from the act's lines so the stored sums match the rows exactly.
                    var all = await store.WorkActLines.Where(l => l.WorkActId == act.Id).ToListAsync(ct);
                    act.TotalHours = Math.Round(all.Sum(l => l.DurationMinutes) / 60m, 2, MidpointRounding.AwayFromZero);
                    act.TotalAmount = Math.Round(all.Sum(l => AmountOf(l.DurationMinutes, l.HourlyRate)), 2, MidpointRounding.AwayFromZero);
                    act.UpdatedAtUtc = DateTime.UtcNow;
                    act.UpdatedByUserId = Guid.Empty;
                    act.Version++;

                    try
                    {
                        await store.SaveChangesAsync(ct);
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        logger.LogWarning("work act {ActId} line edit lost the race", act.Id);
                        return owner.Result.Fragment(_Empty.Create())
                            .WithToast("The act changed while saving; reopen it to see the current state.", title: "Work act", tone: ToastTone.Warning)
                            .WithTrigger("work-acts-list-refresh");
                    }
                }

                // Re-render the row with the saved (or, on failure, submitted) values plus the totals.
                var offset = BrowserTimeZone.GetDisplayOffset(ctx);
                var others = await OtherActsOfEntriesAsync(lines, acts, projects, [line.TimeTrackingId], act.Id, offset, ct);

                var duration = error is null ? line.DurationMinutes : request.DurationMinutes;
                var rate = error is null ? line.HourlyRate : request.HourlyRate;
                var description = (error is null ? line.Description : request.Description)?.Trim() ?? string.Empty;

                var row = new WorkActLineRowModel(
                    line.Id,
                    line.TimeTrackingId,
                    BrowserTimeZone.UtcToWall(line.StartedAtUtc, offset),
                    duration,
                    description,
                    rate,
                    AmountOf(duration, rate),
                    act.CurrencyCode,
                    others.TryGetValue(line.TimeTrackingId, out var other) ? other : [],
                    error);

                return owner.Result.Fragment(_EditRow.Create(new WorkActLineRowViewModel(act.Id, row)))
                    .WithOob("#wa-totals", _Totals.Create(new WorkActTotalsModel(act.TotalHours, act.TotalAmount, act.CurrencyCode)));
            });

            group.MapPost("final/{id:guid}", async (
                Guid id,
                HttpContext ctx,
                IQuery<WorkAct> acts,
                IQuery<Project> projects,
                IQuery<WorkActLine> lines,
                IQuery<InvoiceLine> invoiceLines,
                IWorkActStore store,
                CancellationToken ct) =>
            {
                var act = await store.WorkActs.FirstOrDefaultAsync(a => a.Id == id, ct);
                if (act is null)
                {
                    return Results.NotFound();
                }

                if (act.Status != WorkActStatus.Draft)
                {
                    return owner.Result.Fragment(_Empty.Create())
                        .WithToast("This work act is already final.", title: "Work act", tone: ToastTone.Info);
                }

                act.Status = WorkActStatus.Final;
                act.UpdatedAtUtc = DateTime.UtcNow;
                act.UpdatedByUserId = Guid.Empty;
                act.Version++;

                try
                {
                    await store.SaveChangesAsync(ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    return owner.Result.Fragment(_Empty.Create())
                        .WithToast("The act changed while saving; reopen it to see the current state.", title: "Work act", tone: ToastTone.Warning)
                        .WithTrigger("work-acts-list-refresh");
                }

                var model = await CreateDetailModelAsync(ctx, act, acts, projects, lines, invoiceLines, ct);
                return owner.Result.Fragment(_Detail.Create(model))
                    .WithToast("Work act finalized.", title: "Work act", tone: ToastTone.Success)
                    .WithTrigger("work-acts-list-refresh");
            });
        }
    }

    /// <summary>The Draft act's edit model: its persisted lines with multi-act markers, oldest start first.</summary>
    private static async Task<WorkActReviewModel> CreateEditModelAsync(
        HttpContext ctx,
        WorkAct act,
        IQuery<WorkAct> acts,
        IQuery<Project> projects,
        IQuery<WorkActLine> lines,
        CancellationToken ct)
    {
        var offset = BrowserTimeZone.GetDisplayOffset(ctx);

        var project = await projects.All().FirstOrDefaultAsync(p => p.Id == act.ProjectId, ct);

        var rows = await lines.All()
            .Where(l => l.WorkActId == act.Id)
            .OrderBy(l => l.StartedAtUtc)
            .Select(l => new { l.Id, l.TimeTrackingId, l.StartedAtUtc, l.DurationMinutes, l.Description, l.HourlyRate })
            .ToListAsync(ct);

        var others = await OtherActsOfEntriesAsync(
            lines, acts, projects, rows.Select(r => r.TimeTrackingId).ToArray(), act.Id, offset, ct);

        var lineRows = rows
            .Select(r => new WorkActLineRowModel(
                r.Id,
                r.TimeTrackingId,
                BrowserTimeZone.UtcToWall(r.StartedAtUtc, offset),
                r.DurationMinutes,
                r.Description,
                r.HourlyRate,
                AmountOf(r.DurationMinutes, r.HourlyRate),
                act.CurrencyCode,
                others.TryGetValue(r.TimeTrackingId, out var other) ? other : []))
            .ToArray();

        return new WorkActReviewModel(
            act.Id,
            act.ProjectId,
            project is null ? "(deleted project)" : ProjectLabelOf(project.Code, project.Name),
            act.CurrencyCode,
            DateOnly.FromDateTime(BrowserTimeZone.UtcToWall(act.PeriodStartUtc, offset)),
            DateOnly.FromDateTime(BrowserTimeZone.UtcToWall(act.PeriodEndUtc, offset).AddTicks(-1)),
            lineRows,
            act.TotalHours,
            act.TotalAmount);
    }
}
