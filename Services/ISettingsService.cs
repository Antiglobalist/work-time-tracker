using WorkTimeTracking.Models;

namespace WorkTimeTracking.Services;

public interface ISettingsService
{
    AppSettings Settings { get; }
    void Save();
    void Load();
    event Action? SettingsChanged;
}
