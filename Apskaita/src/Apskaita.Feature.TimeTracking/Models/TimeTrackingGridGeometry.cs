namespace Apskaita.Feature.TimeTracking.Models;

/// <summary>
/// Grid geometry for the week view: slot granularity, pixel scale, work-hours band and the
/// description line clamp. Pure presentation constants — validation bounds live in
/// <see cref="TimeTrackingLimits"/>, data access lives in the endpoints.
/// </summary>
internal static class TimeTrackingGridGeometry
{
    /// <summary>Minutes per grid row (half-hour slots).</summary>
    public const int SlotMinutes = 30;

    /// <summary>Pixels per slot row; entry cards are absolutely positioned against this scale.</summary>
    public const double SlotHeightPx = 34d;

    /// <summary>Start of the visually highlighted work band.</summary>
    public static readonly TimeOnly WorkDayStart = new(8, 0);

    /// <summary>End of the visually highlighted work band.</summary>
    public static readonly TimeOnly WorkDayEnd = new(17, 0);

    /// <summary>Number of slots in a day (24h / <see cref="SlotMinutes"/>).</summary>
    public const int SlotsPerDay = 48;

    /// <summary>
    /// How many description lines fit in a card of <paramref name="cardHeightPx"/>: header padding is
    /// reserved and the rest divided by the line height, clamped to 1..24.
    /// </summary>
    public static int DescriptionLineClamp(double cardHeightPx)
    {
        const double reservedPx = 24d;
        const double lineHeightPx = 14d;
        var lines = (int)Math.Floor((cardHeightPx - reservedPx) / lineHeightPx);
        return Math.Clamp(lines, 1, 24);
    }
}
