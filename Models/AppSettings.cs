namespace WorkTimeTracking.Models;

public class AppSettings
{
    public bool AutoStartWithWindows { get; set; }
    public bool MinimizeToTray { get; set; } = true;
    public string GitRepositoryPath { get; set; } = "";
    public InactivityDetectionMode InactivityMode { get; set; } = InactivityDetectionMode.ScreenOff;
    public int InactivityTimeoutMinutes { get; set; } = 10;
    public AppLanguage AppLanguage { get; set; } = AppLanguage.Auto;

    /// <summary>
    /// Hour (0-23) when a new work day begins. Times before this hour belong to the previous work day.
    /// Default is 0 (midnight), meaning work day = calendar day.
    /// Set to 5 to count times 00:00-04:59 as part of the previous work day.
    /// </summary>
    public int WorkDayStartHour { get; set; } = 0;

    /// <summary>
    /// Minimum continuous activity duration (in minutes) required to classify
    /// an activity session as high focus. Shorter activity sessions are medium focus.
    /// </summary>
    public int HighFocusThresholdMinutes { get; set; } = 60;

    /// <summary>
    /// How much of high-focus time is counted as effective work time (0-100).
    /// </summary>
    public int HighFocusWorkPercent { get; set; } = 95;

    /// <summary>
    /// How much of medium-focus time is counted as effective work time (0-100).
    /// </summary>
    public int MediumFocusWorkPercent { get; set; } = 85;

    /// <summary>
    /// Daily effective work goal in minutes. A notification is shown when reached.
    /// Default is 480 (8 hours). Set to 0 to disable.
    /// </summary>
    public int WorkGoalMinutes { get; set; } = 480;
}
