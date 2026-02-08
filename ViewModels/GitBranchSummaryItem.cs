using System.Linq;
using WorkTimeTracking.Models;
using WorkTimeTracking.Services;

namespace WorkTimeTracking.ViewModels;

public class GitBranchSummaryItem
{
    public string BranchName { get; }
    public string DurationText { get; }
    public bool IsActive { get; }
    public TimeSpan Duration { get; }

    public GitBranchSummaryItem(string branchName, TimeSpan duration, bool isActive, ILocalizationService localizationService)
    {
        BranchName = branchName;
        Duration = duration;
        IsActive = isActive;
        DurationText = localizationService.FormatHoursMinutes(duration);
    }

    public static List<GitBranchSummaryItem> BuildSummaries(
        IEnumerable<GitBranchSession> gitSessions,
        IEnumerable<Session> allSessions,
        DateTime now,
        ILocalizationService localizationService)
    {
        var activitySessions = allSessions.Where(s => s.Type == SessionType.Activity).ToList();

        return gitSessions
            .GroupBy(g => g.BranchName)
            .Select(g =>
            {
                // For each branch, merge all GitBranchSessions into one interval
                // from earliest StartTime to latest EndTime (or now)
                var gitSessionsList = g.ToList();
                var gitStart = gitSessionsList.Min(s => s.StartTime);
                var gitEnd = gitSessionsList.Max(s => s.EndTime ?? now);

                var totalSeconds = 0.0;

                // Calculate overlap between the merged interval and Activity sessions
                foreach (var activity in activitySessions)
                {
                    var activityStart = activity.StartTime;
                    var activityEnd = activity.EndTime ?? now;

                    // Check if there's an overlap
                    if (activityStart < gitEnd && activityEnd > gitStart)
                    {
                        // Calculate the overlap period
                        var overlapStart = activityStart > gitStart ? activityStart : gitStart;
                        var overlapEnd = activityEnd < gitEnd ? activityEnd : gitEnd;

                        totalSeconds += (overlapEnd - overlapStart).TotalSeconds;
                    }
                }

                var duration = TimeSpan.FromSeconds(totalSeconds);
                var isActive = gitSessionsList.Any(s => !s.EndTime.HasValue);
                return new GitBranchSummaryItem(g.Key, duration, isActive, localizationService);
            })
            .OrderByDescending(g => g.Duration.TotalSeconds)
            .ToList();
    }
}
