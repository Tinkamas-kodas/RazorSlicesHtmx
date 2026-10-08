namespace Apskaita.Entities;

public class Project : BaseEntity
{
    public Guid Id { get; set; }
    public Guid ClientId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public int HourlyRate { get; set; }
    public string ColorRgb { get; set; } = "#FF0000";
    public string CurrencyCode { get; set; } = string.Empty;
    public Guid InvoiceTemplateId { get; set; }
    public bool IsActive { get; set; }
}
