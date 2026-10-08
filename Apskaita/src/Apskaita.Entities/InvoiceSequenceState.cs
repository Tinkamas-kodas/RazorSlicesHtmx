namespace Apskaita.Entities;

public enum ResetRule
{
    None,
    Yearly,
    Monthly
}

public class InvoiceSequenceState : BaseEntity
{
    public int Id { get; set; }
    public int CurrentValue { get; set; }
    public string? PrefixContext { get; set; }
    public ResetRule ResetRule { get; set; }
}
