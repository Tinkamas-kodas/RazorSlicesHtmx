using Microsoft.EntityFrameworkCore;
using Apskaita.Entities;

namespace Apskaita.Feature.WorkActs.Persistence;

/// <summary>
/// WorkActs' private write store over the shared <c>DbContext</c>: the <c>work_acts</c> and
/// <c>work_act_lines</c> tables. Declared <c>internal</c> (with <c>InternalsVisibleTo("Apskaita.Web")</c>)
/// so only this feature's endpoints can mutate acts; the time entries the lines reference belong to the
/// time-tracking feature and are never written here.
/// </summary>
internal interface IWorkActStore
{
    DbSet<WorkAct> WorkActs { get; }

    DbSet<WorkActLine> WorkActLines { get; }

    Task<int> SaveChangesAsync(CancellationToken ct);
}
