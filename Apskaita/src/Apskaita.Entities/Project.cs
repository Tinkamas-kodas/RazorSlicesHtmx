namespace Apskaita.Entities;

public class Project : BaseEntity
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public Client Client { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    /// <summary>Required and unique across projects; the database enforces it with a unique index.</summary>
    public string Code { get; set; } = string.Empty;
    public int HourlyRate { get; set; }
    public string ColorRgb { get; set; } = "#FF0000";
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid InvoiceTemplateId { get; set; }
    public InvoiceTemplate InvoiceTemplate { get; set; } = null!;
    public bool IsActive { get; set; }
}
