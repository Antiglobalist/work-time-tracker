using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkTimeTracking.Models;
using WorkTimeTracking.Services;

namespace WorkTimeTracking.ViewModels;

public partial class HistoryViewModel : ObservableObject, IDisposable
{
    private readonly SessionRepository _sessionRepository;
    private readonly INavigationService _navigationService;
    private readonly DispatcherTimer _refreshTimer;
    private readonly ILocalizationService _localizationService;
    private readonly ISettingsService _settingsService;

    public ObservableCollection<DaySummaryItem> Days { get; } = new();

    [ObservableProperty]
    private DaySummaryItem? _selectedDay;

    public HistoryViewModel(SessionRepository sessionRepository, INavigationService navigationService, ILocalizationService localizationService, ISettingsService settingsService)
    {
        _sessionRepository = sessionRepository;
        _navigationService = navigationService;
        _localizationService = localizationService;
        _settingsService = settingsService;
        LoadDays();

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _refreshTimer.Tick += OnRefreshTimerTick;
        _refreshTimer.Start();

        _localizationService.LanguageChanged += OnLanguageChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    private void LoadDays()
    {
        var selectedDate = SelectedDay?.Date;

        Days.Clear();
        var dates = _sessionRepository.GetAllDatesWithSessions();
        foreach (var date in dates)
        {
            var sessions = _sessionRepository.GetSessionsForDate(date);
            var gitSessions = _sessionRepository.GetGitBranchSessionsForDate(date);
            Days.Add(new DaySummaryItem(date, sessions, gitSessions, _localizationService));
        }

        if (Days.Count == 0)
        {
            SelectedDay = null;
            return;
        }

        if (selectedDate.HasValue)
        {
            SelectedDay = Days.FirstOrDefault(d => d.Date.Date == selectedDate.Value.Date) ?? Days[0];
        }
        else
        {
            SelectedDay = Days[0];
        }
    }

    partial void OnSelectedDayChanged(DaySummaryItem? value)
    {
        // Detail view updates via binding
    }

    [RelayCommand]
    private void OpenDayDetail()
    {
        if (SelectedDay != null)
        {
            _navigationService.NavigateTo<DayDetailViewModel>();
            if (_navigationService.CurrentView is DayDetailViewModel detailVm)
            {
                detailVm.LoadDate(SelectedDay.Date);
            }
        }
    }

    private void OnRefreshTimerTick(object? sender, EventArgs e)
    {
        LoadDays();
    }

    private void OnLanguageChanged()
    {
        LoadDays();
    }

    private void OnSettingsChanged()
    {
        LoadDays();
    }

    public void Dispose()
    {
        _refreshTimer.Stop();
        _refreshTimer.Tick -= OnRefreshTimerTick;
        _localizationService.LanguageChanged -= OnLanguageChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;
    }
}

public class DaySummaryItem
{
    public DateTime Date { get; }
    public string DateText { get; }
    public string ActiveTime { get; }
    public string InactiveTime { get; }
    public string TotalTime { get; }
    public string FirstActivityTime { get; }
    public string LastActivityTime { get; }
    public List<SessionDisplayItem> Sessions { get; }
    public List<GitBranchSummaryItem> GitBranchSummaries { get; }

    public DaySummaryItem(DateTime date, List<Session> sessions, List<GitBranchSession> gitSessions, ILocalizationService localizationService)
    {
        Date = date;
        DateText = date.ToString("dd.MM.yyyy, dddd");

        var active = TimeSpan.Zero;
        var inactive = TimeSpan.Zero;
        DateTime? firstActivityStart = null;
        DateTime? lastActivityEnd = null;

        Sessions = new List<SessionDisplayItem>();

        foreach (var s in sessions)
        {
            var duration = s.EndTime.HasValue
                ? s.EndTime.Value - s.StartTime
                : DateTime.Now - s.StartTime;

            if (s.Type == SessionType.Activity)
            {
                active += duration;

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
                inactive += duration;
            }

            Sessions.Add(new SessionDisplayItem(s, localizationService));
        }

        ActiveTime = localizationService.FormatHoursMinutes(active);
        InactiveTime = localizationService.FormatHoursMinutes(inactive);
        TotalTime = localizationService.FormatHoursMinutes(active + inactive);
        FirstActivityTime = firstActivityStart?.ToString("HH:mm") ?? "--:--";
        LastActivityTime = lastActivityEnd?.ToString("HH:mm") ?? "--:--";

        GitBranchSummaries = gitSessions.Count == 0
            ? new List<GitBranchSummaryItem>()
            : GitBranchSummaryItem.BuildSummaries(gitSessions, sessions, DateTime.Now, localizationService);
    }
}
