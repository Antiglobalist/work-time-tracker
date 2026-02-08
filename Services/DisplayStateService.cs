using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace WorkTimeTracking.Services;

public enum DisplayState
{
    Off = 0,
    On = 1,
    Dimmed = 2
}

public sealed class DisplayStateService : IDisposable
{
    private const int WM_POWERBROADCAST = 0x0218;
    private const int PBT_POWERSETTINGCHANGE = 0x8013;
    private const int DEVICE_NOTIFY_WINDOW_HANDLE = 0x00000000;

    private static Guid GUID_CONSOLE_DISPLAY_STATE = new("6FE69556-704A-47A0-8F24-C28D936FDA47");
    private static Guid GUID_VIDEO_SUBGROUP = new("7516B95F-F776-4464-8C53-06167F40CC99");
    private static Guid GUID_VIDEO_POWERDOWN_TIMEOUT = new("3C0BC021-C8A8-4E07-A973-6B14CBCB2B7E");

    private HwndSource? _hwndSource;
    private IntPtr _powerNotifyHandle;
    private bool _disposed;

    public event Action<DisplayState>? DisplayStateChanged;

    public DisplayStateService()
    {
        var parameters = new HwndSourceParameters("DisplayStateMessageWindow")
        {
            Width = 0,
            Height = 0,
            PositionX = -100,
            PositionY = -100,
            WindowStyle = 0,
            ParentWindow = new IntPtr(-3) // HWND_MESSAGE
        };

        _hwndSource = new HwndSource(parameters);
        _hwndSource.AddHook(WndProc);

        _powerNotifyHandle = RegisterPowerSettingNotification(
            _hwndSource.Handle,
            ref GUID_CONSOLE_DISPLAY_STATE,
            DEVICE_NOTIFY_WINDOW_HANDLE);
    }

    public int GetDisplayTimeoutSeconds()
    {
        if (PowerGetActiveScheme(IntPtr.Zero, out var pScheme) != 0 || pScheme == IntPtr.Zero)
            return 0;

        var scheme = Marshal.PtrToStructure<Guid>(pScheme);
        Marshal.FreeHGlobal(pScheme);

        var onAc = IsOnAcPower();
        uint seconds;

        if (onAc)
            PowerReadACValueIndex(IntPtr.Zero, ref scheme, ref GUID_VIDEO_SUBGROUP, ref GUID_VIDEO_POWERDOWN_TIMEOUT, out seconds);
        else
            PowerReadDCValueIndex(IntPtr.Zero, ref scheme, ref GUID_VIDEO_SUBGROUP, ref GUID_VIDEO_POWERDOWN_TIMEOUT, out seconds);

        return (int)seconds;
    }

    private static bool IsOnAcPower()
    {
        if (!GetSystemPowerStatus(out var status))
            return true;

        return status.ACLineStatus == 1;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_POWERBROADCAST && wParam.ToInt32() == PBT_POWERSETTINGCHANGE)
        {
            var setting = Marshal.PtrToStructure<POWERBROADCAST_SETTING>(lParam);
            if (setting.PowerSetting == GUID_CONSOLE_DISPLAY_STATE)
            {
                var state = (DisplayState)setting.Data;
                DisplayStateChanged?.Invoke(state);
                handled = true;
            }
        }
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_powerNotifyHandle != IntPtr.Zero)
        {
            UnregisterPowerSettingNotification(_powerNotifyHandle);
            _powerNotifyHandle = IntPtr.Zero;
        }

        _hwndSource?.RemoveHook(WndProc);
        _hwndSource?.Dispose();
        _hwndSource = null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POWERBROADCAST_SETTING
    {
        public Guid PowerSetting;
        public int DataLength;
        public int Data;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr RegisterPowerSettingNotification(IntPtr hRecipient, ref Guid powerSettingGuid, int flags);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterPowerSettingNotification(IntPtr handle);

    [DllImport("powrprof.dll")]
    private static extern uint PowerGetActiveScheme(IntPtr userRootPowerKey, out IntPtr activePolicyGuid);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadACValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid,
        ref Guid subGroupGuid, ref Guid settingGuid, out uint acValueIndex);

    [DllImport("powrprof.dll")]
    private static extern uint PowerReadDCValueIndex(IntPtr rootPowerKey, ref Guid schemeGuid,
        ref Guid subGroupGuid, ref Guid settingGuid, out uint dcValueIndex);

    [DllImport("kernel32.dll")]
    private static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS status);
}
