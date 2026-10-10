namespace Apskaita.Web.Features.TimeTracking.Models;

[GenerateHtmlNames]
public sealed partial class TimeTrackingModel : IHaveValidationResult
{
    public DateOnly Date { get; set; }

    public TimeOnly Start { get; set; }

    public int DurationMinutes { get; set; }

    public int? HourlyRate { get; set; }

    public Guid? ProjectId { get; set; }

    public string? ProjectLabel { get; set; }

    public string? Description { get; set; }

    public bool Repeat { get; set; }

    public int? RepeatEveryWeeks { get; set; }

    public DateOnly? RepeatUntil { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}
