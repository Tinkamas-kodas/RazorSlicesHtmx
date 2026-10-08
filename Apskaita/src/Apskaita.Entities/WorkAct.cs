namespace Apskaita.Entities;

public enum WorkActStatus
{
    Draft,
    Final,
    Invoiced
}

public class WorkAct : BaseEntity
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid ClientId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal TotalAmount { get; set; }
    public WorkActStatus Status { get; set; }
    public DateTime? InvoicedAtUtc { get; set; }
}
