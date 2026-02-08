using Microsoft.Win32;

namespace WorkTimeTracking.Services;

public class PowerEventService : IDisposable
{
    public event Action? Sleeping;
    public event Action? Resuming;
    public event Action? SessionLocked;
    public event Action? SessionUnlocked;

    public PowerEventService()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.SessionSwitch += OnSessionSwitch;
    }

    private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
    {
        switch (e.Mode)
        {
            case PowerModes.Suspend:
                Sleeping?.Invoke();
                break;
            case PowerModes.Resume:
                Resuming?.Invoke();
                break;
        }
    }

    private void OnSessionSwitch(object sender, SessionSwitchEventArgs e)
    {
        switch (e.Reason)
        {
            case SessionSwitchReason.SessionLock:
                SessionLocked?.Invoke();
                break;
            case SessionSwitchReason.SessionUnlock:
                SessionUnlocked?.Invoke();
                break;
        }
    }

    public void Dispose()
    {
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.SessionSwitch -= OnSessionSwitch;
        GC.SuppressFinalize(this);
    }
}
