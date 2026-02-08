using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkTimeTracking.Models;
using WorkTimeTracking.Services;

namespace WorkTimeTracking.ViewModels;

public partial class DayDetailViewModel : ObservableObject, IDisposable
{
    private readonly SessionRepository _sessionRepository;
    private readonly INavigationService _navigationService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;
    private DateTime? _currentDate;

    [ObservableProperty]
    private string _dateHeader = "";

    [ObservableProperty]
    private string _totalActiveTime = "00:00";

    [ObservableProperty]
    private string _totalInactiveTime = "00:00";

    [ObservableProperty]
    private string _totalDayTime = "00:00";

    [ObservableProperty]
    private string _highFocusTime = "00:00";

    [ObservableProperty]
    private string _mediumFocusTime = "00:00";

    [ObservableProperty]
    private string _effectiveWorkTime = "00:00";

    public ObservableCollection<SessionDisplayItem> Sessions { get; } = new();
    public ObservableCollection<GitBranchSummaryItem> GitBranchSummaries { get; } = new();

    public DayDetailViewModel(
        SessionRepository sessionRepository,
        INavigationService navigationService,
        ILocalizationService localizationService,
        ISettingsService settingsService)
    {
        _sessionRepository = sessionRepository;
        _navigationService = navigationService;
        _localizationService = localizationService;
        _settingsService = settingsService;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();

        _localizationService.LanguageChanged += OnLanguageChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    public void LoadDate(DateTime date)
    {
        _currentDate = date;
        DateHeader = date.ToString("dd MMMM yyyy, dddd");

        var sessions = _sessionRepository.GetSessionsForDate(date);
        var active = TimeSpan.Zero;
        var inactive = TimeSpan.Zero;
        var highFocus = TimeSpan.Zero;
        var mediumFocus = TimeSpan.Zero;
        var thresholdMinutes = _settingsService.Settings.HighFocusThresholdMinutes;
        if (thresholdMinutes <= 0)
            thresholdMinutes = 60;
        var highFocusThreshold = TimeSpan.FromMinutes(thresholdMinutes);
        var highFocusWorkPercent = Math.Clamp(_settingsService.Settings.HighFocusWorkPercent, 0, 100);
        var mediumFocusWorkPercent = Math.Clamp(_settingsService.Settings.MediumFocusWorkPercent, 0, 100);

        Sessions.Clear();
        foreach (var s in sessions)
        {
            var duration = s.EndTime.HasValue
                ? s.EndTime.Value - s.StartTime
                : DateTime.Now - s.StartTime;

            if (s.Type == SessionType.Activity)
            {
                active += duration;
                if (duration >= highFocusThreshold)
                    highFocus += duration;
                else
                    mediumFocus += duration;
            }
            else
            {
                inactive += duration;
            }

            Sessions.Add(new SessionDisplayItem(s, _localizationService));
        }

        TotalActiveTime = _localizationService.FormatHoursMinutes(active);
        TotalInactiveTime = _localizationService.FormatHoursMinutes(inactive);
        HighFocusTime = _localizationService.FormatHoursMinutes(highFocus);
        MediumFocusTime = _localizationService.FormatHoursMinutes(mediumFocus);
        var effectiveSeconds =
            (highFocus.TotalSeconds * highFocusWorkPercent / 100.0) +
            (mediumFocus.TotalSeconds * mediumFocusWorkPercent / 100.0);
        EffectiveWorkTime = _localizationService.FormatHoursMinutes(TimeSpan.FromSeconds(effectiveSeconds));
        var total = active + inactive;
        TotalDayTime = _localizationService.FormatHoursMinutes(total);

        var gitSessions = _sessionRepository.GetGitBranchSessionsForDate(date);
        GitBranchSummaries.Clear();
        if (gitSessions.Count > 0)
        {
            var grouped = GitBranchSummaryItem.BuildSummaries(gitSessions, sessions, DateTime.Now, _localizationService);
            foreach (var item in grouped)
                GitBranchSummaries.Add(item);
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        _navigationService.NavigateTo<HistoryViewModel>();
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        if (_currentDate.HasValue)
            LoadDate(_currentDate.Value);
    }

    private void OnLanguageChanged()
    {
        if (_currentDate.HasValue)
            LoadDate(_currentDate.Value);
    }

    private void OnSettingsChanged()
    {
        if (_currentDate.HasValue)
            LoadDate(_currentDate.Value);
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}
