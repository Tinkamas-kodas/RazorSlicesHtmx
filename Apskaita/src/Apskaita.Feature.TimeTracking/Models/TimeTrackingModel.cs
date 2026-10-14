namespace Apskaita.Feature.TimeTracking.Models;

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

    /// <summary>Calendar event reference parts (set when the dialog was opened from an event); persisted as the owned <c>CalendarEventReference</c>.</summary>
    public string? EventProviderKey { get; set; }

    public string? EventAccountKey { get; set; }

    public string? EventId { get; set; }

    /// <summary>True when the dialog originates from a calendar event: hides recurrence and carries the hidden reference fields.</summary>
    public bool FromCalendarEvent { get; set; }

    public IReadOnlyDictionary<string, (string errorCode, string errorMessage)[]> Errors { get; set; } =
        new Dictionary<string, (string errorCode, string errorMessage)[]>();
}
