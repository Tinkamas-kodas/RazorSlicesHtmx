using Apskaita.Entities.Contracts;
using Apskaita.Feature.WorkActs.Models;
using Apskaita.Feature.WorkActs.Persistence;
using Apskaita.Feature.WorkActs.Slices;
using Apskaita.Shared;
using Apskaita.Shared.Routing;
using Microsoft.EntityFrameworkCore;

namespace Apskaita.Feature.WorkActs.Endpoints;

public sealed partial class WorkActEndpoints
{
    /// <summary>
    /// Act detail (header plus its lines, read-only, with the multi-act marker) and deletion. Only a Draft
    /// act can be deleted; deleting removes the act and its lines and leaves the referenced entries alone.
    /// </summary>
    private sealed class DetailSection(WorkActEndpoints owner)
    {
        public void Map(RouteGroupBuilder group)
        {
            group.MapGet("detail/{id:guid}", async (
                Guid id,
                HttpContext ctx,
                IFeatureUri uri,
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

                var model = await CreateDetailModelAsync(ctx, act, acts, projects, lines, invoiceLines, ct);
                return ctx.IsHtmxTarget("work-acts-detail")
                    ? owner.Result.Fragment(_Detail.Create(model))
                    : owner.Result.Page(_Detail.Create(model));
            });

            group.MapGet("delete/{id:guid}", async (
                Guid id,
                HttpContext ctx,
                IQuery<WorkAct> acts,
                IQuery<Project> projects,
                CancellationToken ct) =>
            {
                var act = await acts.All().FirstOrDefaultAsync(a => a.Id == id, ct);
                if (act is null)
                {
                    return Results.NotFound();
                }

                var project = await projects.All().FirstOrDefaultAsync(p => p.Id == act.ProjectId, ct);
                var offset = BrowserTimeZone.GetDisplayOffset(ctx);

                var model = new WorkActDeleteModel(
                    act.Id,
                    project is null ? "(deleted project)" : ProjectLabelOf(project.Code, project.Name),
                    BrowserTimeZone.UtcToWall(act.PeriodStartUtc, offset),
                    BrowserTimeZone.UtcToWall(act.PeriodEndUtc, offset).AddTicks(-1));

                return owner.Result.Fragment(_DeleteDialog.Create(model));
            });

            group.MapPost("delete/{id:guid}", async (
                Guid id,
                IWorkActStore store,
                CancellationToken ct) =>
            {
                var act = await store.WorkActs.FirstOrDefaultAsync(a => a.Id == id, ct);
                if (act is null)
                {
                    return Results.NotFound();
                }

                // Final and invoiced acts are documents; they are kept, not deleted.
                if (act.Status != WorkActStatus.Draft)
                {
                    return owner.Result.Fragment(_Empty.Create())
                        .ClearDialog()
                        .WithToast("Final and invoiced work acts cannot be deleted.", title: "Work act", tone: ToastTone.Warning);
                }

                var lines = await store.WorkActLines.Where(l => l.WorkActId == id).ToListAsync(ct);
                store.WorkActLines.RemoveRange(lines);
                store.WorkActs.Remove(act);
                await store.SaveChangesAsync(ct);

                return owner.Result.Fragment(_Empty.Create())
                    .ClearDialog()
                    .WithTrigger("work-acts-list-refresh")
                    .WithToast("Work act was deleted.", title: "Work act", tone: ToastTone.Warning);
            });
        }
    }

    /// <summary>
    /// The detail model, shared by the detail route and the edit section's fallback for frozen acts: the
    /// act's lines with their multi-act markers and the effective (invoice-derived) status label.
    /// </summary>
    private static async Task<WorkActDetailModel> CreateDetailModelAsync(
        HttpContext ctx,
        WorkAct act,
        IQuery<WorkAct> acts,
        IQuery<Project> projects,
        IQuery<WorkActLine> lines,
        IQuery<InvoiceLine> invoiceLines,
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

        return new WorkActDetailModel(
            act.Id,
            project is null ? "(deleted project)" : ProjectLabelOf(project.Code, project.Name),
            BrowserTimeZone.UtcToWall(act.PeriodStartUtc, offset),
            // The stored end is exclusive; show the last covered day.
            BrowserTimeZone.UtcToWall(act.PeriodEndUtc, offset).AddTicks(-1),
            act.TotalHours,
            act.TotalAmount,
            act.CurrencyCode,
            await EffectiveStatusLabelAsync(invoiceLines, act, ct),
            act.Status == WorkActStatus.Draft,
            BrowserTimeZone.UtcToWall(act.CreatedAtUtc, offset),
            lineRows);
    }
}
