using System.Collections.ObjectModel;
using System.Globalization;
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

    private List<DaySummaryItem> _allDays = new();
    private bool _isUpdatingFilters;

    public ObservableCollection<DaySummaryItem> Days { get; } = new();

    [ObservableProperty]
    private DaySummaryItem? _selectedDay;

    public ObservableCollection<int> AvailableYears { get; } = new();
    public ObservableCollection<string> AvailableMonths { get; } = new();

    [ObservableProperty]
    private int? _selectedYear;

    [ObservableProperty]
    private string? _selectedMonth;

    [ObservableProperty]
    private bool _sortAscending;

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

        _allDays.Clear();
        var dates = _sessionRepository.GetAllDatesWithSessions();
        foreach (var date in dates)
        {
            var sessions = _sessionRepository.GetSessionsForDate(date);
            var gitSessions = _sessionRepository.GetGitBranchSessionsForDate(date);
            _allDays.Add(new DaySummaryItem(date, sessions, gitSessions, _localizationService, _settingsService));
        }

        UpdateAvailableFilters();
        ApplyFilter();

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

    private void UpdateAvailableFilters()
    {
        _isUpdatingFilters = true;

        var previousYear = SelectedYear;
        var previousMonth = SelectedMonth;

        var years = _allDays.Select(d => d.Date.Year).Distinct().OrderByDescending(y => y).ToList();
        AvailableYears.Clear();
        foreach (var y in years)
            AvailableYears.Add(y);

        if (previousYear.HasValue && AvailableYears.Contains(previousYear.Value))
            SelectedYear = previousYear.Value;
        else if (AvailableYears.Count > 0)
            SelectedYear = AvailableYears[0];
        else
            SelectedYear = null;

        UpdateAvailableMonths();

        if (previousMonth != null && AvailableMonths.Contains(previousMonth))
            SelectedMonth = previousMonth;
        else if (AvailableMonths.Count > 0)
            SelectedMonth = AvailableMonths[0];
        else
            SelectedMonth = null;

        _isUpdatingFilters = false;
    }

    private void UpdateAvailableMonths()
    {
        var previousMonth = SelectedMonth;
        AvailableMonths.Clear();

        if (!SelectedYear.HasValue)
            return;

        var months = _allDays
            .Where(d => d.Date.Year == SelectedYear.Value)
            .Select(d => d.Date.Month)
            .Distinct()
            .OrderByDescending(m => m)
            .ToList();

        foreach (var m in months)
        {
            var name = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(m);
            name = char.ToUpper(name[0]) + name.Substring(1);
            AvailableMonths.Add(name);
        }

        if (previousMonth != null && AvailableMonths.Contains(previousMonth))
            SelectedMonth = previousMonth;
        else if (AvailableMonths.Count > 0)
            SelectedMonth = AvailableMonths[0];
        else
            SelectedMonth = null;
    }

    private int? GetMonthNumberFromName(string? monthName)
    {
        if (string.IsNullOrEmpty(monthName))
            return null;

        for (int i = 1; i <= 12; i++)
        {
            var name = CultureInfo.CurrentCulture.DateTimeFormat.GetMonthName(i);
            name = char.ToUpper(name[0]) + name.Substring(1);
            if (name == monthName)
                return i;
        }
        return null;
    }

    private void ApplyFilter()
    {
        var selectedDate = SelectedDay?.Date;
        Days.Clear();

        var filtered = _allDays.AsEnumerable();

        if (SelectedYear.HasValue)
            filtered = filtered.Where(d => d.Date.Year == SelectedYear.Value);

        var monthNumber = GetMonthNumberFromName(SelectedMonth);
        if (monthNumber.HasValue)
            filtered = filtered.Where(d => d.Date.Month == monthNumber.Value);

        var sorted = SortAscending
            ? filtered.OrderBy(d => d.Date)
            : filtered.OrderByDescending(d => d.Date);

        foreach (var day in sorted)
            Days.Add(day);

        if (Days.Count == 0)
        {
            SelectedDay = null;
            return;
        }

        if (selectedDate.HasValue)
            SelectedDay = Days.FirstOrDefault(d => d.Date.Date == selectedDate.Value.Date) ?? Days[0];
        else
            SelectedDay = Days[0];
    }

    partial void OnSelectedYearChanged(int? value)
    {
        if (_isUpdatingFilters) return;

        _isUpdatingFilters = true;
        UpdateAvailableMonths();
        _isUpdatingFilters = false;

        ApplyFilter();
    }

    partial void OnSelectedMonthChanged(string? value)
    {
        if (_isUpdatingFilters) return;
        ApplyFilter();
    }

    partial void OnSortAscendingChanged(bool value)
    {
        ApplyFilter();
    }

    [RelayCommand]
    private void ToggleSort()
    {
        SortAscending = !SortAscending;
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
    public string EffectiveWorkTime { get; }
    public string FirstActivityTime { get; }
    public string LastActivityTime { get; }
    public List<SessionDisplayItem> Sessions { get; }
    public List<GitBranchSummaryItem> GitBranchSummaries { get; }

    public DaySummaryItem(DateTime date, List<Session> sessions, List<GitBranchSession> gitSessions, ILocalizationService localizationService, ISettingsService settingsService)
    {
        Date = date;
        DateText = date.ToString("dd.MM.yyyy, dddd");

        var active = TimeSpan.Zero;
        var inactive = TimeSpan.Zero;
        var highFocus = TimeSpan.Zero;
        var mediumFocus = TimeSpan.Zero;
        DateTime? firstActivityStart = null;
        DateTime? lastActivityEnd = null;

        var thresholdMinutes = settingsService.Settings.HighFocusThresholdMinutes;
        if (thresholdMinutes <= 0) thresholdMinutes = 60;
        var highFocusThreshold = TimeSpan.FromMinutes(thresholdMinutes);
        var highFocusWorkPercent = Math.Clamp(settingsService.Settings.HighFocusWorkPercent, 0, 100);
        var mediumFocusWorkPercent = Math.Clamp(settingsService.Settings.MediumFocusWorkPercent, 0, 100);

        Sessions = new List<SessionDisplayItem>();

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

        var effectiveSeconds =
            (highFocus.TotalSeconds * highFocusWorkPercent / 100.0) +
            (mediumFocus.TotalSeconds * mediumFocusWorkPercent / 100.0);
        EffectiveWorkTime = localizationService.FormatHoursMinutes(TimeSpan.FromSeconds(effectiveSeconds));

        FirstActivityTime = firstActivityStart?.ToString("HH:mm") ?? "--:--";
        LastActivityTime = lastActivityEnd?.ToString("HH:mm") ?? "--:--";

        GitBranchSummaries = gitSessions.Count == 0
            ? new List<GitBranchSummaryItem>()
            : GitBranchSummaryItem.BuildSummaries(gitSessions, sessions, DateTime.Now, localizationService);
    }
}
