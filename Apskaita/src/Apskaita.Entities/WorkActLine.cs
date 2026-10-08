namespace Apskaita.Entities;

public enum WorkActLineSource
{
    Clockify,
    Manual
}

public class WorkActLine : BaseEntity
{
    public Guid Id { get; set; }
    public Guid WorkActId { get; set; }
    public string? ExternalId { get; set; }
    public WorkActLineSource Source { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateOnly WorkDate { get; set; }
    public int DurationInMinutes { get; set; }
    public string? Notes { get; set; }
    public int HourlyRate { get; set; }
    public decimal Amount { get; set; }
}
