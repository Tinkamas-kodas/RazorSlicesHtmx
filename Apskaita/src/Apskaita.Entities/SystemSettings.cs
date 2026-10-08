namespace Apskaita.Entities;

public class SystemSettings : BaseEntity
{
    public int Id { get; set; }
    public decimal DefaultVatRatePercent { get; set; }
    public string InvoiceNumberTemplate { get; set; } = string.Empty;
}
