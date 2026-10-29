using WorkTimeTracking.Models;

namespace WorkTimeTracking.Helpers;

/// <summary>
/// Helper class for merging consecutive sessions with the same type and reason
/// </summary>
public static class SessionMergeHelper
{
    /// <summary>
    /// Merges consecutive sessions that have the same Type and Reason
    /// </summary>
    /// <param name="sessions">List of sessions sorted by StartTime</param>
    /// <returns>List of merged sessions</returns>
    public static List<Session> MergeConsecutiveSessions(List<Session> sessions)
    {
        if (sessions == null || sessions.Count == 0)
            return new List<Session>();

        var merged = new List<Session>();
        Session? currentMerged = null;

        foreach (var session in sessions)
        {
            if (currentMerged == null)
            {
                // First session - create a copy
                currentMerged = CreateSessionCopy(session);
                continue;
            }

            // Check if this session should be merged with the current one
            if (ShouldMerge(currentMerged, session))
            {
                // Extend the end time of the merged session
                currentMerged.EndTime = session.EndTime;
            }
            else
            {
                // Different type/reason or not consecutive - save current and start new
                merged.Add(currentMerged);
                currentMerged = CreateSessionCopy(session);
            }
        }

        // Add the last merged session
        if (currentMerged != null)
        {
            merged.Add(currentMerged);
        }

        return merged;
    }

    /// <summary>
    /// Clips sessions so that every moment is counted once (sessions sorted by StartTime).
    /// </summary>
    public static List<Session> TrimOverlaps(List<Session> sessions)
    {
        // Several running app instances used to write parallel sessions for the same time.
        var result = new List<Session>(sessions.Count);
        var coveredUntil = DateTime.MinValue;

        foreach (var session in sessions)
        {
            var end = session.EndTime ?? DateTime.MaxValue;
            if (end <= coveredUntil)
                continue;

            if (session.StartTime < coveredUntil)
                session.StartTime = coveredUntil;

            result.Add(session);
            coveredUntil = end;
        }

        return result;
    }

    /// <summary>
    /// Determines if two consecutive sessions should be merged
    /// </summary>
    private static bool ShouldMerge(Session first, Session second)
    {
        // If first session doesn't have an end time, don't merge
        if (!first.EndTime.HasValue)
            return false;

        // Check if sessions are consecutive (gap less than 10 seconds)
        var gap = second.StartTime - first.EndTime.Value;
        if (gap.TotalSeconds > 10)
            return false;

        // Check if sessions represent the same state
        return AreSameState(first, second);
    }

    /// <summary>
    /// Determines if two sessions represent the same state for merging purposes
    /// </summary>
    private static bool AreSameState(Session first, Session second)
    {
        // Both are Sleep-related (Type=Sleep OR Reason=Sleep)
        bool firstIsSleep = first.Type == SessionType.Sleep || first.Reason == SessionReason.Sleep;
        bool secondIsSleep = second.Type == SessionType.Sleep || second.Reason == SessionReason.Sleep;

        if (firstIsSleep && secondIsSleep)
            return true;

        // Otherwise, must have exactly the same Type and Reason
        return first.Type == second.Type && first.Reason == second.Reason;
    }

    /// <summary>
    /// Creates a copy of a session (to avoid modifying the original)
    /// </summary>
    private static Session CreateSessionCopy(Session session)
    {
        return new Session
        {
            Id = session.Id,
            StartTime = session.StartTime,
            EndTime = session.EndTime,
            Type = session.Type,
            Reason = session.Reason
        };
    }
}
