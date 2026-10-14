namespace Apskaita.Shared;

/// <summary>
/// Browser-reported UTC offset. The client (<c>browser-timezone.js</c>) writes its timezone offset into
/// a cookie on every page load; the server uses it to render calendar events and time entries in the
/// visitor's zone instead of the server's. An absent or invalid cookie means "fall back to the
/// server's local zone".
/// </summary>
public static class BrowserTimeZone
{
    /// <summary>Cookie name; value is minutes ahead of UTC (Vilnius summer = 180, i.e. <c>-getTimezoneOffset()</c>).</summary>
    public const string CookieName = "tz-offset";

    /// <summary>Reads and validates the browser offset cookie; false when absent or outside [-12h, +14h].</summary>
    public static bool TryGetOffset(HttpContext context, out TimeSpan offset)
    {
        offset = TimeSpan.Zero;
        if (!context.Request.Cookies.TryGetValue(CookieName, out var raw) || !int.TryParse(raw, out var minutes))
        {
            return false;
        }

        // Real-world UTC offsets span -12:00..+14:00; anything else is corrupt or tampered.
        if (minutes < -720 || minutes > 840)
        {
            return false;
        }

        offset = TimeSpan.FromMinutes(minutes);
        return true;
    }

    /// <summary>
    /// Display-zone offset for a request: the browser's cookie offset when present, otherwise the
    /// server's local offset. Used to convert stored UTC values to/from display wall time.
    /// </summary>
    public static TimeSpan GetDisplayOffset(HttpContext context)
        => TryGetOffset(context, out var offset) ? offset : TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow);

    /// <summary>Display-zone wall time → naive UTC value (utc = wall − offset).</summary>
    public static DateTime WallToUtc(DateTime wallClock, TimeSpan offset) => wallClock - offset;

    /// <summary>Naive UTC value → display-zone wall time (wall = utc + offset).</summary>
    public static DateTime UtcToWall(DateTime utc, TimeSpan offset) => utc + offset;
}
