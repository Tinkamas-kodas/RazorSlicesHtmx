namespace Apskaita.Entities;

public enum InvoiceLineSourceType
{
    WorkAct,
    Manual
}

public class InvoiceLine : BaseEntity
{
    public Guid Id { get; set; }
    public Guid InvoiceId { get; set; }
    public int LineNo { get; set; }
    public InvoiceLineSourceType SourceType { get; set; }
    public Guid? WorkActId { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public Guid? UnitId { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal Amount { get; set; }
    public bool VatApplicable { get; set; }
    public decimal VatRatePercent { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
