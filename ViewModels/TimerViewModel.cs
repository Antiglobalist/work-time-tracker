using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkTimeTracking.Helpers;
using WorkTimeTracking.Models;
using WorkTimeTracking.Services;

namespace WorkTimeTracking.ViewModels;

public partial class TimerViewModel : ObservableObject, IDisposable
{
    private readonly ActivityTrackingService _trackingService;
    private readonly SessionRepository _sessionRepository;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty]
    private bool _isTracking;

    [ObservableProperty]
    private string _statusText = "";

    [ObservableProperty]
    private string _totalActiveTime = "00:00";

    [ObservableProperty]
    private string _totalInactiveTime = "00:00";

    [ObservableProperty]
    private string _lastActivity = "--:--";

    [ObservableProperty]
    private string _firstActivityTime = "--:--";

    [ObservableProperty]
    private string _lastActivityTime = "--:--";

    [ObservableProperty]
    private bool _showEndTime = false;

    [ObservableProperty]
    private string _toggleButtonText = "";

    public ObservableCollection<SessionDisplayItem> Sessions { get; } = new();
    public ObservableCollection<GitBranchSummaryItem> GitBranchSummaries { get; } = new();

    public TimerViewModel(ActivityTrackingService trackingService, SessionRepository sessionRepository, ILocalizationService localizationService, ISettingsService settingsService)
    {
        _trackingService = trackingService;
        _sessionRepository = sessionRepository;
        _localizationService = localizationService;
        _settingsService = settingsService;

        _trackingService.StateChanged += RefreshData;
        _localizationService.LanguageChanged += OnLanguageChanged;
        _settingsService.SettingsChanged += RefreshData;

        IsTracking = _trackingService.IsTracking;
        UpdateStatusUI();
        RefreshData();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        RefreshData();
    }

    [RelayCommand]
    private void ToggleTracking()
    {
        if (_trackingService.IsTracking)
        {
            _trackingService.Stop();
        }
        else
        {
            _trackingService.Start();
        }

        IsTracking = _trackingService.IsTracking;
        UpdateStatusUI();
        RefreshData();
    }

    private void UpdateStatusUI()
    {
        StatusText = IsTracking ? _localizationService["StatusRunning"] : _localizationService["StatusStopped"];
        ToggleButtonText = IsTracking ? _localizationService["ToggleStop"] : _localizationService["ToggleStart"];
    }

    private void RefreshData()
    {
        // Use work day instead of calendar day
        var currentWorkDay = WorkDayHelper.GetWorkDay(DateTime.Now);
        var sessions = _sessionRepository.GetSessionsForDate(currentWorkDay);

        // Don't show end time for current day (day is still ongoing)
        ShowEndTime = false;

        var activeTime = TimeSpan.Zero;
        var inactiveTime = TimeSpan.Zero;
        DateTime? firstActivityStart = null;
        DateTime? lastActivityEnd = null;

        foreach (var s in sessions)
        {
            var duration = s.EndTime.HasValue
                ? s.EndTime.Value - s.StartTime
                : DateTime.Now - s.StartTime;

            if (s.Type == SessionType.Activity)
            {
                activeTime += duration;

                // Track first activity start
                if (!firstActivityStart.HasValue || s.StartTime < firstActivityStart.Value)
                    firstActivityStart = s.StartTime;

                // Track last activity end
                var end = s.EndTime ?? DateTime.Now;
                if (!lastActivityEnd.HasValue || end > lastActivityEnd.Value)
                    lastActivityEnd = end;
            }
            else
            {
                inactiveTime += duration;
            }
        }

        TotalActiveTime = _localizationService.FormatHoursMinutes(activeTime);
        TotalInactiveTime = _localizationService.FormatHoursMinutes(inactiveTime);
        LastActivity = lastActivityEnd?.ToString("HH:mm") ?? "--:--";
        FirstActivityTime = firstActivityStart?.ToString("HH:mm") ?? "--:--";
        LastActivityTime = lastActivityEnd?.ToString("HH:mm") ?? "--:--";

        Sessions.Clear();
        foreach (var s in sessions)
        {
            Sessions.Add(new SessionDisplayItem(s, _localizationService));
        }

        // Git branch summaries
        var gitSessions = _sessionRepository.GetGitBranchSessionsForDate(currentWorkDay);
        GitBranchSummaries.Clear();
        if (gitSessions.Count > 0)
        {
            var grouped = GitBranchSummaryItem.BuildSummaries(gitSessions, sessions, DateTime.Now, _localizationService);
            foreach (var item in grouped)
                GitBranchSummaries.Add(item);
        }
    }

    private void OnLanguageChanged()
    {
        UpdateStatusUI();
        RefreshData();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _trackingService.StateChanged -= RefreshData;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _settingsService.SettingsChanged -= RefreshData;
    }

}

public class SessionDisplayItem
{
    public string TimeRange { get; }
    public string TypeText { get; }
    public string DurationText { get; }
    public SessionType Type { get; }
    public bool IsActivity { get; }
    public bool IsInactivity { get; }

    public SessionDisplayItem(Session session, ILocalizationService localizationService)
    {
        Type = session.Type;
        IsActivity = session.Type == SessionType.Activity;
        IsInactivity = !IsActivity;

        var start = session.StartTime.ToString("HH:mm");
        var end = session.EndTime?.ToString("HH:mm") ?? DateTime.Now.ToString("HH:mm");
        TimeRange = $"{start} - {end}";

        TypeText = session.Type == SessionType.Activity
            ? localizationService["SessionActivityPrefix"]
            : localizationService["SessionInactivityPrefix"];

        var duration = session.EndTime.HasValue
            ? session.EndTime.Value - session.StartTime
            : DateTime.Now - session.StartTime;

        if (session.Type == SessionType.Activity)
        {
            DurationText = localizationService.FormatHoursMinutes(duration);
        }
        else if (session.Type == SessionType.Sleep)
        {
            DurationText = localizationService["ReasonSleep"];
        }
        else
        {
            var reasonText = session.Reason switch
            {
                SessionReason.Sleep => localizationService["ReasonSleep"],
                SessionReason.HelloRejected => localizationService["ReasonHelloRejected"],
                SessionReason.HelloTimeout => localizationService["ReasonHelloTimeout"],
                SessionReason.ScreenOff => localizationService["ReasonScreenOff"],
                _ => localizationService.FormatHoursMinutes(duration)
            };
            DurationText = reasonText;
        }
    }
}

