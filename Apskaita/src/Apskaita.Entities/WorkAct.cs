namespace Apskaita.Entities;

public enum WorkActStatus
{
    Draft,
    Final,
    Invoiced
}

/// <summary>
/// A work act ("darbų aktas"): one project's work certified for a period. Its content is its
/// <see cref="WorkActLine"/> rows — frozen copies of the time entries they reference — so the act keeps
/// its own numbers even if an entry later joins another act. Totals are computed from the lines and
/// refreshed on every save.
/// <para>
/// <see cref="Status"/> is <see cref="WorkActStatus.Draft"/> while the lines are editable,
/// <see cref="WorkActStatus.Final"/> once finalized (lines frozen), and presented as
/// <see cref="WorkActStatus.Invoiced"/> when an invoice line references the act (derived at read time).
/// </para>
/// <para>
/// The period is stored as naive UTC instants derived from the local date boundaries the user chose
/// (<see cref="PeriodEndUtc"/> is the exclusive midnight after the last day), so the covered entry set
/// never depends on who views the act. There is no act number — an act is identified by project and period.
/// </para>
/// </summary>
public class WorkAct : BaseEntity
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    /// <summary>Denormalized from the project so an act can be filtered and invoiced per client.</summary>
    public Guid ClientId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public DateTime PeriodStartUtc { get; set; }
    public DateTime PeriodEndUtc { get; set; }
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
    public WorkActStatus Status { get; set; }
    public DateTime? InvoicedAtUtc { get; set; }

    /// <summary>Optimistic concurrency token; incremented on every save.</summary>
    public int Version { get; set; }
}
