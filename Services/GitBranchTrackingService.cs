using System.Diagnostics;
using System.IO;
using System.Timers;
using WorkTimeTracking.Helpers;
using WorkTimeTracking.Models;

namespace WorkTimeTracking.Services;

public sealed class GitBranchTrackingService : IDisposable
{
    private readonly ActivityTrackingService _trackingService;
    private readonly SessionRepository _sessionRepository;
    private readonly ISettingsService _settingsService;
    private readonly System.Timers.Timer _pollTimer;
    private readonly object _lock = new();

    private GitBranchSession? _currentBranchSession;
    private string? _currentRepoPath;
    private string? _currentBranch;
    private bool _isRunning;
    private bool _disposed;

    public GitBranchTrackingService(
        ActivityTrackingService trackingService,
        SessionRepository sessionRepository,
        ISettingsService settingsService)
    {
        _trackingService = trackingService;
        _sessionRepository = sessionRepository;
        _settingsService = settingsService;

        _pollTimer = new System.Timers.Timer(30000);
        _pollTimer.AutoReset = true;
        _pollTimer.Elapsed += OnPollElapsed;

        _trackingService.StateChanged += OnTrackingStateChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    public void Start()
    {
        if (_isRunning) return;
        _isRunning = true;

        // Close any orphaned open sessions from previous runs
        _sessionRepository.CloseAllOpenGitBranchSessions(DateTime.Now);

        _pollTimer.Start();
        EvaluateState();
    }

    public void Stop()
    {
        if (!_isRunning) return;
        _isRunning = false;
        _pollTimer.Stop();
        EndCurrentBranchSession();
    }

    private void OnPollElapsed(object? sender, ElapsedEventArgs e)
    {
        EvaluateState();
    }

    private void OnTrackingStateChanged()
    {
        EvaluateState();
    }

    private void OnSettingsChanged()
    {
        EvaluateState();
    }

    private void EvaluateState()
    {
        if (_disposed || !_isRunning) return;

        lock (_lock)
        {
            if (_disposed || !_isRunning) return;

            // Don't check if tracking is active - we track branch regardless of activity/inactivity
            // Only stop tracking if the service is stopped or settings are invalid
            if (!_trackingService.IsTracking)
            {
                EndCurrentBranchSession();
                return;
            }

            var repoPath = (_settingsService.Settings.GitRepositoryPath ?? "").Trim();

            if (string.IsNullOrWhiteSpace(repoPath))
            {
                EndCurrentBranchSession();
                return;
            }

            if (!Directory.Exists(repoPath))
            {
                EndCurrentBranchSession();
                return;
            }

            var branch = GetCurrentBranch(repoPath);
            if (string.IsNullOrWhiteSpace(branch))
            {
                EndCurrentBranchSession();
                return;
            }

            // Check if we need to close the current session because the work day changed
            if (_currentBranchSession != null)
            {
                var currentWorkDay = WorkDayHelper.GetWorkDay(DateTime.Now);
                var sessionWorkDay = WorkDayHelper.GetWorkDay(_currentBranchSession.StartTime);

                if (currentWorkDay != sessionWorkDay)
                {
                    // New work day started - close old session and create new one
                    EndCurrentBranchSession();
                    StartNewBranchSession(repoPath, branch);
                    return;
                }
            }

            // Create new session only if branch changed or no current session
            if (_currentBranchSession == null ||
                !PathsEqual(repoPath, _currentRepoPath) ||
                !string.Equals(branch, _currentBranch, StringComparison.Ordinal))
            {
                EndCurrentBranchSession();
                StartNewBranchSession(repoPath, branch);
            }
        }
    }

    private void StartNewBranchSession(string repoPath, string branch)
    {
        _currentBranchSession = new GitBranchSession
        {
            StartTime = DateTime.Now,
            RepoPath = repoPath,
            BranchName = branch
        };
        _sessionRepository.AddGitBranchSession(_currentBranchSession);
        _currentRepoPath = repoPath;
        _currentBranch = branch;
    }

    private void EndCurrentBranchSession()
    {
        if (_currentBranchSession == null) return;

        _sessionRepository.CloseGitBranchSession(_currentBranchSession.Id, DateTime.Now);
        _currentBranchSession = null;
        _currentRepoPath = null;
        _currentBranch = null;
    }

    private static string? GetCurrentBranch(string repoPath)
    {
        try
        {
            var branch = RunGit(repoPath, "rev-parse --abbrev-ref HEAD");
            if (string.IsNullOrWhiteSpace(branch))
                return null;

            if (string.Equals(branch, "HEAD", StringComparison.OrdinalIgnoreCase))
            {
                var sha = RunGit(repoPath, "rev-parse --short HEAD");
                if (string.IsNullOrWhiteSpace(sha))
                    return "DETACHED";
                return $"DETACHED@{sha}";
            }

            return branch;
        }
        catch
        {
            return null;
        }
    }

    private static string? RunGit(string repoPath, string args)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = args,
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        if (!process.Start())
            return null;

        if (!process.WaitForExit(2000))
        {
            try { process.Kill(); } catch { }
            return null;
        }

        if (process.ExitCode != 0)
            return null;

        var output = process.StandardOutput.ReadToEnd().Trim();
        return string.IsNullOrWhiteSpace(output) ? null : output;
    }

    private static bool PathsEqual(string? left, string? right)
    {
        if (left == null || right == null) return false;
        var l = Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var r = Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return string.Equals(l, r, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Stop();
        _pollTimer.Elapsed -= OnPollElapsed;
        _trackingService.StateChanged -= OnTrackingStateChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _pollTimer.Dispose();
        GC.SuppressFinalize(this);
    }
}
