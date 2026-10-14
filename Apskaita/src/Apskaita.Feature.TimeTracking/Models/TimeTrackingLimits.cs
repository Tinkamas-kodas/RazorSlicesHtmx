namespace Apskaita.Feature.TimeTracking.Models;

/// <summary>
/// Shared limits for the time-tracking feature. This is the single source of truth used by both
/// <c>TimeTrackingModelValidator</c> (validation rules) and <c>TimeTrackingContentService</c>
/// (occurrence generation and clamping), so validation and creation behaviour cannot silently desync.
/// </summary>
public static class TimeTrackingLimits
{
    // Duration (minutes).
    public const int MinDurationMinutes = 1;
    public const int MaxDurationMinutes = 1440;
    public const int DefaultDurationMinutes = 30;

    // Weekly recurrence.
    public const int DaysPerWeek = 7;
    public const int MinRepeatWeeks = 1;
    public const int MaxRepeatWeeks = 104;

    /// <summary>Maximum span (days) between the entry date and <c>RepeatUntil</c> (two years).</summary>
    public const int MaxRepeatSpanDays = 730;

    /// <summary>
    /// Ceiling on generated occurrences: the most a valid <c>RepeatUntil</c> can ever request
    /// (the base entry plus span ÷ smallest interval). Generation honors all of these, so this is a
    /// safety backstop rather than a truncation of a valid end date.
    /// </summary>
    public const int MaxOccurrences = MaxRepeatSpanDays / (MinRepeatWeeks * DaysPerWeek) + 1; // 105
}
