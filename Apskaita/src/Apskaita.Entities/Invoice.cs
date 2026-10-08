namespace Apskaita.Entities;

public enum InvoiceStatus
{
    Draft,
    Issued,
    Cancelled
}

public class Invoice : BaseEntity
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public Guid IssuerId { get; set; }
    public CompanyInfo IssuerInfo { get; set; } = new();
    public Guid ClientId { get; set; }
    public CompanyInfo ClientInfo { get; set; } = new();
    public Guid? ProjectId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string LanguageCode { get; set; } = string.Empty;
    public Guid InvoiceTemplateId { get; set; }
    public DateOnly IssueDate { get; set; }
    public DateOnly? DueDate { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal VatAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public string TotalAmountInText { get; set; } = string.Empty;
    public InvoiceStatus Status { get; set; }
    public string? Notes { get; set; }
}
