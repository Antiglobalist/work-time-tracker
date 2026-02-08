using System.Globalization;
using System.Windows;
using WorkTimeTracking.Models;

namespace WorkTimeTracking.Services;

public sealed class LocalizationService : ILocalizationService
{
    private readonly ISettingsService _settingsService;
    private const string ResourcePrefix = "Resources/StringResources.";

    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.InvariantCulture;
    public event Action? LanguageChanged;

    public LocalizationService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public void ApplyLanguage()
    {
        var targetCulture = ResolveCulture();
        ApplyCulture(targetCulture);
    }

    public string this[string key]
    {
        get
        {
            if (System.Windows.Application.Current == null)
                return key;

            var value = System.Windows.Application.Current.TryFindResource(key);
            return value as string ?? key;
        }
    }

    public string FormatHoursMinutes(TimeSpan ts)
    {
        var hoursLabel = this["HourShort"];
        var minutesLabel = this["MinuteShort"];
        var h = (int)ts.TotalHours;
        var m = ts.Minutes;
        return h > 0 ? $"{h}{hoursLabel} {m:D2}{minutesLabel}" : $"{m}{minutesLabel}";
    }

    private CultureInfo ResolveCulture()
    {
        var setting = _settingsService.Settings.AppLanguage;
        return setting switch
        {
            AppLanguage.Russian => new CultureInfo("ru-RU"),
            AppLanguage.English => new CultureInfo("en-US"),
            _ => GetSystemCulture()
        };
    }

    private static CultureInfo GetSystemCulture()
    {
        var ui = CultureInfo.CurrentUICulture;
        return string.Equals(ui.TwoLetterISOLanguageName, "ru", StringComparison.OrdinalIgnoreCase)
            ? new CultureInfo("ru-RU")
            : new CultureInfo("en-US");
    }

    private void ApplyCulture(CultureInfo culture)
    {
        CurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        if (System.Windows.Application.Current == null)
            return;

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"{ResourcePrefix}{culture.Name}.xaml", UriKind.Relative)
        };

        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var resources = System.Windows.Application.Current.Resources.MergedDictionaries;
            for (var i = resources.Count - 1; i >= 0; i--)
            {
                var source = resources[i].Source?.OriginalString ?? "";
                if (source.StartsWith(ResourcePrefix, StringComparison.OrdinalIgnoreCase))
                    resources.RemoveAt(i);
            }

            resources.Add(dictionary);
        });

        LanguageChanged?.Invoke();
    }
}
