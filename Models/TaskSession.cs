using System.ComponentModel.DataAnnotations;
using WorkTimeTracking.Helpers;

namespace WorkTimeTracking.Models;

public class TaskSession
{
    [Key]
    public int Id { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public TimeSpan Duration => EndTime.HasValue
        ? EndTime.Value - StartTime
        : DateTime.Now - StartTime;

    /// <summary>
    /// Total time the user was away from PC during this task session (sleep/lock periods).
    /// Subtracted from Duration to get net work time.
    /// </summary>
    public TimeSpan AwayTime { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Net working time = Duration - AwayTime
    /// </summary>
    public TimeSpan WorkTime => Duration - AwayTime;

    public DateTime Date => WorkDayHelper.GetWorkDay(StartTime);
}
