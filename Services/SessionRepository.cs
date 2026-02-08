using Microsoft.EntityFrameworkCore;
using WorkTimeTracking.Data;
using WorkTimeTracking.Helpers;
using WorkTimeTracking.Models;

namespace WorkTimeTracking.Services;

public class SessionRepository
{
    private readonly Func<AppDbContext> _contextFactory;

    public SessionRepository(Func<AppDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public void EnsureDatabase()
    {
        using var ctx = _contextFactory();
        ctx.Database.EnsureCreated();

        // EnsureCreated won't add new tables to an existing DB.
        // Manually create TaskSessions if missing.
        using var connection = ctx.Database.GetDbConnection();
        connection.Open();
        using var cmd = connection.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE IF NOT EXISTS TaskSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StartTime TEXT NOT NULL,
                EndTime TEXT,
                AwayTime TEXT NOT NULL DEFAULT '00:00:00'
            );
            CREATE INDEX IF NOT EXISTS IX_TaskSessions_StartTime ON TaskSessions (StartTime);

            CREATE TABLE IF NOT EXISTS GitBranchSessions (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                StartTime TEXT NOT NULL,
                EndTime TEXT,
                RepoPath TEXT NOT NULL,
                BranchName TEXT NOT NULL
            );
            CREATE INDEX IF NOT EXISTS IX_GitBranchSessions_StartTime ON GitBranchSessions (StartTime);
            CREATE INDEX IF NOT EXISTS IX_GitBranchSessions_BranchName ON GitBranchSessions (BranchName);
            CREATE INDEX IF NOT EXISTS IX_GitBranchSessions_RepoPath ON GitBranchSessions (RepoPath);
            """;
        cmd.ExecuteNonQuery();
    }

    public void AddSession(Session session)
    {
        using var ctx = _contextFactory();
        ctx.Sessions.Add(session);
        ctx.SaveChanges();
    }

    public void UpdateSession(Session session)
    {
        using var ctx = _contextFactory();
        ctx.Sessions.Update(session);
        ctx.SaveChanges();
    }

    public List<Session> GetSessionsForDate(DateTime workDayDate)
    {
        using var ctx = _contextFactory();
        // Work day starts at configured hour, not midnight
        var start = workDayDate.Date.AddHours(WorkDayHelper.WorkDayStartHour);
        var end = start.AddDays(1);
        return ctx.Sessions
            .Where(s => s.StartTime >= start && s.StartTime < end)
            .OrderBy(s => s.StartTime)
            .AsNoTracking()
            .ToList();
    }

    public List<DateTime> GetAllDatesWithSessions()
    {
        using var ctx = _contextFactory();
        return ctx.Sessions
            .AsNoTracking()
            .Select(s => s.StartTime.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();
    }

    public Session? GetOpenSession()
    {
        using var ctx = _contextFactory();
        return ctx.Sessions
            .FirstOrDefault(s => s.EndTime == null);
    }

    public void CloseSession(int sessionId, DateTime endTime)
    {
        using var ctx = _contextFactory();
        var session = ctx.Sessions.Find(sessionId);
        if (session != null)
        {
            session.EndTime = endTime;
            ctx.SaveChanges();
        }
    }

    // --- TaskSession ---

    public void AddTaskSession(TaskSession taskSession)
    {
        using var ctx = _contextFactory();
        ctx.TaskSessions.Add(taskSession);
        ctx.SaveChanges();
    }

    public void CloseTaskSession(int taskSessionId, DateTime endTime)
    {
        using var ctx = _contextFactory();
        var ts = ctx.TaskSessions.Find(taskSessionId);
        if (ts != null)
        {
            ts.EndTime = endTime;
            ctx.SaveChanges();
        }
    }

    public void AddAwayTimeToTaskSession(int taskSessionId, TimeSpan awayTime)
    {
        using var ctx = _contextFactory();
        var ts = ctx.TaskSessions.Find(taskSessionId);
        if (ts != null)
        {
            ts.AwayTime += awayTime;
            ctx.SaveChanges();
        }
    }

    public TaskSession? GetOpenTaskSession()
    {
        using var ctx = _contextFactory();
        return ctx.TaskSessions.FirstOrDefault(t => t.EndTime == null);
    }

    public List<TaskSession> GetTaskSessionsForDate(DateTime workDayDate)
    {
        using var ctx = _contextFactory();
        // Work day starts at configured hour, not midnight
        var start = workDayDate.Date.AddHours(WorkDayHelper.WorkDayStartHour);
        var end = start.AddDays(1);
        return ctx.TaskSessions
            .Where(t => t.StartTime >= start && t.StartTime < end)
            .OrderBy(t => t.StartTime)
            .AsNoTracking()
            .ToList();
    }

    // --- GitBranchSession ---

    public void AddGitBranchSession(GitBranchSession gitSession)
    {
        using var ctx = _contextFactory();
        ctx.GitBranchSessions.Add(gitSession);
        ctx.SaveChanges();
    }

    public void CloseGitBranchSession(int gitSessionId, DateTime endTime)
    {
        using var ctx = _contextFactory();
        var gs = ctx.GitBranchSessions.Find(gitSessionId);
        if (gs != null)
        {
            gs.EndTime = endTime;
            ctx.SaveChanges();
        }
    }

    public GitBranchSession? GetOpenGitBranchSession()
    {
        using var ctx = _contextFactory();
        return ctx.GitBranchSessions.FirstOrDefault(g => g.EndTime == null);
    }

    public List<GitBranchSession> GetGitBranchSessionsForDate(DateTime workDayDate)
    {
        using var ctx = _contextFactory();
        // Work day starts at configured hour, not midnight
        var start = workDayDate.Date.AddHours(WorkDayHelper.WorkDayStartHour);
        var end = start.AddDays(1);
        return ctx.GitBranchSessions
            .Where(g => g.StartTime >= start && g.StartTime < end)
            .OrderBy(g => g.StartTime)
            .AsNoTracking()
            .ToList();
    }

    public void CloseAllOpenGitBranchSessions(DateTime endTime)
    {
        using var ctx = _contextFactory();
        var openSessions = ctx.GitBranchSessions
            .Where(g => g.EndTime == null)
            .ToList();

        foreach (var session in openSessions)
        {
            session.EndTime = endTime;
        }

        ctx.SaveChanges();
    }

    public void CloseAllOpenSessions(DateTime endTime)
    {
        using var ctx = _contextFactory();
        var openSessions = ctx.Sessions
            .Where(s => s.EndTime == null)
            .ToList();

        foreach (var session in openSessions)
        {
            session.EndTime = endTime;
        }

        ctx.SaveChanges();
    }
}
