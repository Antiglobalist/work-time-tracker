using System.Globalization;

namespace WorkTimeTracking.Services;

public interface ILocalizationService
{
    CultureInfo CurrentCulture { get; }
    event Action? LanguageChanged;
    string this[string key] { get; }
    void ApplyLanguage();
    string FormatHoursMinutes(TimeSpan ts);
}
