namespace WorkTimeTracking.Helpers;

/// <summary>
/// Helper for calculating work day dates based on a configurable start hour.
/// </summary>
public static class WorkDayHelper
{
    /// <summary>
    /// Current work day start hour setting. Updated when settings change.
    /// </summary>
    public static int WorkDayStartHour { get; set; } = 0;

    /// <summary>
    /// Calculates the work day date for a given timestamp.
    /// If the time is before WorkDayStartHour, it belongs to the previous calendar day.
    /// </summary>
    /// <param name="timestamp">The timestamp to calculate work day for</param>
    /// <returns>The work day date</returns>
    /// <example>
    /// WorkDayStartHour = 5
    /// 2026-02-04 02:00 → 2026-02-03 (before 5am, so previous day)
    /// 2026-02-04 08:00 → 2026-02-04 (after 5am, so current day)
    /// </example>
    public static DateTime GetWorkDay(DateTime timestamp)
    {
        // If time is before the work day start hour, it belongs to the previous day
        if (timestamp.Hour < WorkDayStartHour)
        {
            return timestamp.Date.AddDays(-1);
        }

        return timestamp.Date;
    }
}
