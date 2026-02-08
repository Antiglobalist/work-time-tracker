using System.ComponentModel.DataAnnotations;
using WorkTimeTracking.Helpers;

namespace WorkTimeTracking.Models;

public class Session
{
    [Key]
    public int Id { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public TimeSpan Duration => EndTime.HasValue
        ? EndTime.Value - StartTime
        : DateTime.Now - StartTime;

    public SessionType Type { get; set; }
    public SessionReason Reason { get; set; }

    public DateTime Date => WorkDayHelper.GetWorkDay(StartTime);
}
