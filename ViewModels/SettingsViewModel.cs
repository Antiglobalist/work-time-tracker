using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WorkTimeTracking.Helpers;
using WorkTimeTracking.Models;
using WorkTimeTracking.Services;

namespace WorkTimeTracking.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;

    [ObservableProperty]
    private bool _autoStartWithWindows;

    [ObservableProperty]
    private bool _minimizeToTray;

    [ObservableProperty]
    private string _gitRepositoryPath = "";

    [ObservableProperty]
    private bool _useScreenOffDetection;

    [ObservableProperty]
    private bool _useTimeoutDetection;

    [ObservableProperty]
    private int _selectedTimeoutIndex;

    [ObservableProperty]
    private int _selectedLanguageIndex;

    [ObservableProperty]
    private int _selectedWorkDayStartIndex;

    [ObservableProperty]
    private int _selectedHighFocusThresholdIndex;

    [ObservableProperty]
    private int _selectedHighFocusWorkPercentIndex;

    [ObservableProperty]
    private int _selectedMediumFocusWorkPercentIndex;

    [ObservableProperty]
    private int _selectedWorkGoalIndex;

    public ObservableCollection<InactivityTimeoutOption> TimeoutOptions { get; } = new();
    public ObservableCollection<LanguageOption> LanguageOptions { get; } = new();
    public ObservableCollection<WorkDayStartOption> WorkDayStartOptions { get; } = new();
    public ObservableCollection<HighFocusThresholdOption> HighFocusThresholdOptions { get; } = new();
    public ObservableCollection<FocusEfficiencyOption> FocusEfficiencyOptions { get; } = new();
    public ObservableCollection<WorkGoalOption> WorkGoalOptions { get; } = new();

    public SettingsViewModel(ISettingsService settingsService, ILocalizationService localizationService)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _localizationService.LanguageChanged += OnLanguageChanged;

        BuildTimeoutOptions();
        BuildLanguageOptions();
        BuildWorkDayStartOptions();
        BuildHighFocusThresholdOptions();
        BuildFocusEfficiencyOptions();
        BuildWorkGoalOptions();
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = _settingsService.Settings;
        AutoStartWithWindows = s.AutoStartWithWindows;
        MinimizeToTray = s.MinimizeToTray;
        GitRepositoryPath = s.GitRepositoryPath ?? "";
        UseScreenOffDetection = s.InactivityMode == InactivityDetectionMode.ScreenOff;
        UseTimeoutDetection = s.InactivityMode == InactivityDetectionMode.TimeoutMinutes;

        SelectedTimeoutIndex = TimeoutOptions
            .Select((o, i) => new { o.Minutes, i })
            .FirstOrDefault(x => x.Minutes == s.InactivityTimeoutMinutes)?.i ?? 2;
        if (SelectedTimeoutIndex < 0) SelectedTimeoutIndex = 2; // default 10 min

        SelectedLanguageIndex = s.AppLanguage switch
        {
            AppLanguage.Russian => 1,
            AppLanguage.English => 2,
            _ => 0
        };

        SelectedWorkDayStartIndex = WorkDayStartOptions
            .Select((o, i) => new { o.Hour, i })
            .FirstOrDefault(x => x.Hour == s.WorkDayStartHour)?.i ?? 0;
        if (SelectedWorkDayStartIndex < 0) SelectedWorkDayStartIndex = 0; // default 0:00

        SelectedHighFocusThresholdIndex = HighFocusThresholdOptions
            .Select((o, i) => new { o.Minutes, i })
            .FirstOrDefault(x => x.Minutes == s.HighFocusThresholdMinutes)?.i ?? 1;
        if (SelectedHighFocusThresholdIndex < 0) SelectedHighFocusThresholdIndex = 1; // default 60 min

        SelectedHighFocusWorkPercentIndex = FindClosestFocusEfficiencyIndex(s.HighFocusWorkPercent, 95);
        SelectedMediumFocusWorkPercentIndex = FindClosestFocusEfficiencyIndex(s.MediumFocusWorkPercent, 85);

        SelectedWorkGoalIndex = WorkGoalOptions
            .Select((o, i) => new { o.Minutes, i })
            .FirstOrDefault(x => x.Minutes == s.WorkGoalMinutes)?.i ?? 5;
        if (SelectedWorkGoalIndex < 0) SelectedWorkGoalIndex = 5;
    }

    [RelayCommand]
    private void Save()
    {
        var s = _settingsService.Settings;
        s.AutoStartWithWindows = AutoStartWithWindows;
        s.MinimizeToTray = MinimizeToTray;
        s.GitRepositoryPath = (GitRepositoryPath ?? "").Trim();
        s.InactivityMode = UseTimeoutDetection ? InactivityDetectionMode.TimeoutMinutes : InactivityDetectionMode.ScreenOff;
        s.InactivityTimeoutMinutes = TimeoutOptions[SelectedTimeoutIndex].Minutes;
        s.AppLanguage = SelectedLanguageIndex switch
        {
            1 => AppLanguage.Russian,
            2 => AppLanguage.English,
            _ => AppLanguage.Auto
        };

        // Validate index before accessing collection
        if (SelectedWorkDayStartIndex < 0 || SelectedWorkDayStartIndex >= WorkDayStartOptions.Count)
            SelectedWorkDayStartIndex = 0;

        s.WorkDayStartHour = WorkDayStartOptions[SelectedWorkDayStartIndex].Hour;

        if (SelectedHighFocusThresholdIndex < 0 || SelectedHighFocusThresholdIndex >= HighFocusThresholdOptions.Count)
            SelectedHighFocusThresholdIndex = 1;

        s.HighFocusThresholdMinutes = HighFocusThresholdOptions[SelectedHighFocusThresholdIndex].Minutes;

        if (SelectedHighFocusWorkPercentIndex < 0 || SelectedHighFocusWorkPercentIndex >= FocusEfficiencyOptions.Count)
            SelectedHighFocusWorkPercentIndex = FindClosestFocusEfficiencyIndex(95, 95);

        if (SelectedMediumFocusWorkPercentIndex < 0 || SelectedMediumFocusWorkPercentIndex >= FocusEfficiencyOptions.Count)
            SelectedMediumFocusWorkPercentIndex = FindClosestFocusEfficiencyIndex(85, 85);

        s.HighFocusWorkPercent = FocusEfficiencyOptions[SelectedHighFocusWorkPercentIndex].Percent;
        s.MediumFocusWorkPercent = FocusEfficiencyOptions[SelectedMediumFocusWorkPercentIndex].Percent;

        if (SelectedWorkGoalIndex < 0 || SelectedWorkGoalIndex >= WorkGoalOptions.Count)
            SelectedWorkGoalIndex = 5;
        s.WorkGoalMinutes = WorkGoalOptions[SelectedWorkGoalIndex].Minutes;

        // Update the static helper with the new setting
        WorkDayHelper.WorkDayStartHour = s.WorkDayStartHour;

        _settingsService.Save();
        _localizationService.ApplyLanguage();

        if (AutoStartWithWindows)
            SetAutoStart(true);
        else
            SetAutoStart(false);
    }

    partial void OnUseScreenOffDetectionChanged(bool value)
    {
        if (value && UseTimeoutDetection)
            UseTimeoutDetection = false;
        if (!value && !UseTimeoutDetection)
            UseTimeoutDetection = true;
    }

    partial void OnUseTimeoutDetectionChanged(bool value)
    {
        if (value && UseScreenOffDetection)
            UseScreenOffDetection = false;
        if (!value && !UseScreenOffDetection)
            UseScreenOffDetection = true;
    }

    [RelayCommand]
    private void BrowseGitRepository()
    {
        using var dialog = new System.Windows.Forms.FolderBrowserDialog
        {
            Description = _localizationService["SelectGitProject"],
            UseDescriptionForTitle = true,
            SelectedPath = string.IsNullOrWhiteSpace(GitRepositoryPath)
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
                : GitRepositoryPath
        };

        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
        {
            GitRepositoryPath = dialog.SelectedPath;
        }
    }

    private static void SetAutoStart(bool enable)
    {
        const string appName = "WorkTimeTracking";
        var exePath = Environment.ProcessPath ?? "";
        using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);

        if (key == null) return;

        if (enable && !string.IsNullOrEmpty(exePath))
            key.SetValue(appName, $"\"{exePath}\"");
        else
            key.DeleteValue(appName, false);
    }

    private void BuildTimeoutOptions()
    {
        TimeoutOptions.Clear();
        var min = _localizationService["MinuteShort"];
        TimeoutOptions.Add(new InactivityTimeoutOption(2, $"2 {min}"));
        TimeoutOptions.Add(new InactivityTimeoutOption(5, $"5 {min}"));
        TimeoutOptions.Add(new InactivityTimeoutOption(10, $"10 {min}"));
        TimeoutOptions.Add(new InactivityTimeoutOption(15, $"15 {min}"));
        TimeoutOptions.Add(new InactivityTimeoutOption(30, $"30 {min}"));
        TimeoutOptions.Add(new InactivityTimeoutOption(60, $"60 {min}"));
    }

    private void BuildLanguageOptions()
    {
        LanguageOptions.Clear();
        LanguageOptions.Add(new LanguageOption(AppLanguage.Auto, _localizationService["LanguageAuto"]));
        LanguageOptions.Add(new LanguageOption(AppLanguage.Russian, _localizationService["LanguageRussian"]));
        LanguageOptions.Add(new LanguageOption(AppLanguage.English, _localizationService["LanguageEnglish"]));
    }

    private void BuildWorkDayStartOptions()
    {
        WorkDayStartOptions.Clear();
        for (int hour = 0; hour < 24; hour++)
        {
            var label = $"{hour:D2}:00";
            WorkDayStartOptions.Add(new WorkDayStartOption(hour, label));
        }
    }

    private void BuildHighFocusThresholdOptions()
    {
        HighFocusThresholdOptions.Clear();
        var min = _localizationService["MinuteShort"];
        var hour = _localizationService["HourShort"];
        HighFocusThresholdOptions.Add(new HighFocusThresholdOption(30, $"30 {min}"));
        HighFocusThresholdOptions.Add(new HighFocusThresholdOption(60, $"1 {hour}"));
        HighFocusThresholdOptions.Add(new HighFocusThresholdOption(120, $"2 {hour}"));
        HighFocusThresholdOptions.Add(new HighFocusThresholdOption(180, $"3 {hour}"));
    }

    private void BuildFocusEfficiencyOptions()
    {
        FocusEfficiencyOptions.Clear();
        for (var percent = 50; percent <= 100; percent += 5)
        {
            FocusEfficiencyOptions.Add(new FocusEfficiencyOption(percent, $"{percent}%"));
        }
    }

    private void BuildWorkGoalOptions()
    {
        WorkGoalOptions.Clear();
        var h = _localizationService["HourShort"];
        var m = _localizationService["MinuteShort"];
        WorkGoalOptions.Add(new WorkGoalOption(0, _localizationService["WorkGoalDisabled"]));
        WorkGoalOptions.Add(new WorkGoalOption(360, $"6 {h}"));
        WorkGoalOptions.Add(new WorkGoalOption(390, $"6 {h} 30 {m}"));
        WorkGoalOptions.Add(new WorkGoalOption(420, $"7 {h}"));
        WorkGoalOptions.Add(new WorkGoalOption(450, $"7 {h} 30 {m}"));
        WorkGoalOptions.Add(new WorkGoalOption(480, $"8 {h}"));
        WorkGoalOptions.Add(new WorkGoalOption(510, $"8 {h} 30 {m}"));
        WorkGoalOptions.Add(new WorkGoalOption(540, $"9 {h}"));
        WorkGoalOptions.Add(new WorkGoalOption(600, $"10 {h}"));
    }

    private int FindClosestFocusEfficiencyIndex(int value, int fallbackPercent)
    {
        if (FocusEfficiencyOptions.Count == 0)
            return 0;

        var fallbackIndex = FocusEfficiencyOptions
            .Select((o, i) => new { o.Percent, i })
            .FirstOrDefault(x => x.Percent == fallbackPercent)?.i ?? 0;

        var item = FocusEfficiencyOptions
            .Select((o, i) => new { o.Percent, i, Distance = Math.Abs(o.Percent - value) })
            .OrderBy(x => x.Distance)
            .FirstOrDefault();

        return item?.i ?? fallbackIndex;
    }

    private void OnLanguageChanged()
    {
        BuildTimeoutOptions();
        BuildLanguageOptions();
        BuildHighFocusThresholdOptions();
        BuildFocusEfficiencyOptions();
        BuildWorkGoalOptions();
        LoadSettings();
    }

    public void Dispose()
    {
        _localizationService.LanguageChanged -= OnLanguageChanged;
    }
}

public record InactivityTimeoutOption(int Minutes, string Label)
{
    public override string ToString() => Label;
}

public record LanguageOption(AppLanguage Language, string Label)
{
    public override string ToString() => Label;
}

public record WorkDayStartOption(int Hour, string Label)
{
    public override string ToString() => Label;
}

public record HighFocusThresholdOption(int Minutes, string Label)
{
    public override string ToString() => Label;
}

public record FocusEfficiencyOption(int Percent, string Label)
{
    public override string ToString() => Label;
}

public record WorkGoalOption(int Minutes, string Label)
{
    public override string ToString() => Label;
}
  