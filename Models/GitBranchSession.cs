using System.ComponentModel.DataAnnotations;
using WorkTimeTracking.Helpers;

namespace WorkTimeTracking.Models;

public class GitBranchSession
{
    [Key]
    public int Id { get; set; }

    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }

    public string RepoPath { get; set; } = "";
    public string BranchName { get; set; } = "";

    public TimeSpan Duration => EndTime.HasValue
        ? EndTime.Value - StartTime
        : DateTime.Now - StartTime;

    public DateTime Date => WorkDayHelper.GetWorkDay(StartTime);
}
