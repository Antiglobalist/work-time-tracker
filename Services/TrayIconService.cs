using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace WorkTimeTracking.Services;

/// <summary>
/// System tray icon wrapper based on WinForms NotifyIcon.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private NotifyIcon? _notifyIcon;
    private bool _disposed;

    public event Action? LeftClick;
    public event Action? RightClick;

    public void Create(string tooltip)
    {
        if (_notifyIcon != null) return;

        _notifyIcon = new NotifyIcon
        {
            Text = TruncateTooltip(tooltip),
            Icon = CreateIcon(),
            Visible = true
        };

        _notifyIcon.MouseClick += OnMouseClick;
    }

    public void SetTooltip(string tooltip)
    {
        if (_notifyIcon == null) return;
        _notifyIcon.Text = TruncateTooltip(tooltip);
    }

    private void OnMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            LeftClick?.Invoke();
        else if (e.Button == MouseButtons.Right)
            RightClick?.Invoke();
    }

    private static Icon CreateIcon()
    {
        using var bmp = new Bitmap(32, 32, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            using var brush = new SolidBrush(Color.FromArgb(64, 160, 43));
            g.FillEllipse(brush, 2, 2, 28, 28);

            using var pen = new Pen(Color.White, 2.5f);
            g.DrawLine(pen, 16, 16, 16, 7);
            g.DrawLine(pen, 16, 16, 23, 16);
        }

        var hIcon = bmp.GetHicon();
        var icon = Icon.FromHandle(hIcon);
        var cloned = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        icon.Dispose();
        return cloned;
    }

    private static string TruncateTooltip(string tooltip)
    {
        if (string.IsNullOrWhiteSpace(tooltip))
            return "WorkTimeTracking";

        return tooltip.Length <= 63 ? tooltip : tooltip[..63];
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        if (_notifyIcon != null)
        {
            _notifyIcon.MouseClick -= OnMouseClick;
            _notifyIcon.Visible = false;
            _notifyIcon.Icon?.Dispose();
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
