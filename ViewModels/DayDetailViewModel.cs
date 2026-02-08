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
    private DateTime? _currentDate;

    [ObservableProperty]
    private string _dateHeader = "";

    [ObservableProperty]
    private string _totalActiveTime = "00:00";

    [ObservableProperty]
    private string _totalInactiveTime = "00:00";

    [ObservableProperty]
    private string _totalDayTime = "00:00";

    public ObservableCollection<SessionDisplayItem> Sessions { get; } = new();
    public ObservableCollection<GitBranchSummaryItem> GitBranchSummaries { get; } = new();

    public DayDetailViewModel(SessionRepository sessionRepository, INavigationService navigationService, ILocalizationService localizationService)
    {
        _sessionRepository = sessionRepository;
        _navigationService = navigationService;
        _localizationService = localizationService;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();

        _localizationService.LanguageChanged += OnLanguageChanged;
    }

    public void LoadDate(DateTime date)
    {
        _currentDate = date;
        DateHeader = date.ToString("dd MMMM yyyy, dddd");

        var sessions = _sessionRepository.GetSessionsForDate(date);
        var active = TimeSpan.Zero;
        var inactive = TimeSpan.Zero;

        Sessions.Clear();
        foreach (var s in sessions)
        {
            var duration = s.EndTime.HasValue
                ? s.EndTime.Value - s.StartTime
                : DateTime.Now - s.StartTime;

            if (s.Type == SessionType.Activity)
                active += duration;
            else
                inactive += duration;

            Sessions.Add(new SessionDisplayItem(s, _localizationService));
        }

        TotalActiveTime = _localizationService.FormatHoursMinutes(active);
        TotalInactiveTime = _localizationService.FormatHoursMinutes(inactive);
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

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }
}
