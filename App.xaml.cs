using System.ComponentModel;
using System.IO;
using System.Windows;
using Application = System.Windows.Application;
using Microsoft.Extensions.DependencyInjection;
using WorkTimeTracking.Data;
using WorkTimeTracking.Helpers;
using WorkTimeTracking.Services;
using WorkTimeTracking.ViewModels;

namespace WorkTimeTracking;

public partial class App : Application
{
    private static IServiceProvider _services = null!;
    public static IServiceProvider Services => _services;

    private PowerEventService? _powerEventService;
    private ActivityTrackingService? _trackingService;
    private GitBranchTrackingService? _gitBranchTrackingService;
    private TrayIconService? _trayIcon;
    private DisplayStateService? _displayStateService;
    private ISettingsService? _settingsService;
    private ILocalizationService? _localizationService;
    private bool _isExiting;
    private MainWindow? _mainWindow;
    private static readonly string StartupLogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkTimeTracking", "startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogStartup("OnStartup begin");

        DispatcherUnhandledException += (_, args) =>
        {
            LogStartup("DispatcherUnhandledException", args.Exception);
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            LogStartup("AppDomain.UnhandledException", args.ExceptionObject as Exception);
        };

        try
        {
            var serviceCollection = new ServiceCollection();
            ConfigureServices(serviceCollection);
            _services = serviceCollection.BuildServiceProvider();
            LogStartup("Services built");

            _settingsService = _services.GetRequiredService<ISettingsService>();
            _settingsService.Load();
            LogStartup("Settings loaded");

            // Initialize work day helper with the current setting
            WorkDayHelper.WorkDayStartHour = _settingsService.Settings.WorkDayStartHour;
            LogStartup($"WorkDayHelper initialized with start hour: {WorkDayHelper.WorkDayStartHour}");

            _localizationService = _services.GetRequiredService<ILocalizationService>();
            _localizationService.ApplyLanguage();
            _localizationService.LanguageChanged += UpdateLocalizedUi;
            LogStartup("Localization applied");

            // Setup tray icon early so user can always access the app.
            SetupTrayIcon();
            LogStartup("Tray icon created");

            // Initialize database
            var repo = _services.GetRequiredService<SessionRepository>();
            repo.EnsureDatabase();
            LogStartup("Database ensured");

            // Setup power events
            _powerEventService = _services.GetRequiredService<PowerEventService>();
            _trackingService = _services.GetRequiredService<ActivityTrackingService>();
            _displayStateService = _services.GetRequiredService<DisplayStateService>();
            _gitBranchTrackingService = _services.GetRequiredService<GitBranchTrackingService>();
            LogStartup("Services resolved");

            _powerEventService.Sleeping += () => _trackingService.OnSleep();
            _powerEventService.Resuming += () => _trackingService.OnWake();
            _powerEventService.SessionLocked += () => _trackingService.OnSleep();
            _powerEventService.SessionUnlocked += () => _trackingService.OnWake();

            // Auto-start tracking so all PC time is captured
            _trackingService.Start();
            _gitBranchTrackingService.Start();
            LogStartup("Tracking started");

            // Show main window
            ShowMainWindow();
            LogStartup("Main window shown");
        }
        catch (Exception ex)
        {
            LogStartup("Startup error", ex);
            System.Windows.MessageBox.Show($"Startup error: {ex}", "WorkTimeTracking", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Data
        services.AddTransient<AppDbContext>();
        services.AddSingleton<Func<AppDbContext>>(sp => () => sp.GetRequiredService<AppDbContext>());
        services.AddSingleton<SessionRepository>();

        // Services
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<InputHookService>();
        services.AddSingleton<DisplayStateService>();
        services.AddSingleton<ActivityTrackingService>();
        services.AddSingleton<WindowsHelloService>();
        services.AddSingleton<GitBranchTrackingService>();
        services.AddSingleton<PowerEventService>();

        // ViewModels
        services.AddTransient<TimerViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<DayDetailViewModel>();
        services.AddTransient<SettingsViewModel>();
        services.AddSingleton<MainViewModel>();

        // Windows
        services.AddSingleton<MainWindow>();
    }

    private void SetupTrayIcon()
    {
        if (_trayIcon != null) return;

        _trayIcon = new TrayIconService();
        var tooltip = _localizationService?["TrayTooltip"] ?? "WorkTimeTracking";
        _trayIcon.Create(tooltip);
        _trayIcon.LeftClick += ShowMainWindow;
        _trayIcon.RightClick += ShowTrayContextMenu;
    }

    private void UpdateLocalizedUi()
    {
        var tooltip = _localizationService?["TrayTooltip"] ?? "WorkTimeTracking";
        _trayIcon?.SetTooltip(tooltip);
    }

    private void ShowTrayContextMenu()
    {
        var menu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "Открыть" };
        openItem.Click += (_, _) => ShowMainWindow();
        menu.Items.Add(openItem);

        menu.Items.Add(new System.Windows.Controls.Separator());

        var exitItem = new System.Windows.Controls.MenuItem { Header = "Выход" };
        exitItem.Click += (_, _) => RealShutdown();
        menu.Items.Add(exitItem);

        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void ShowMainWindow()
    {
        try
        {
            var window = EnsureMainWindow();
            window.ShowInTaskbar = true;
            if (!window.IsVisible)
                window.Show();
            if (window.WindowState != WindowState.Normal)
                window.WindowState = WindowState.Normal;
            window.Activate();
        }
        catch (Exception ex)
        {
            LogStartup("ShowMainWindow failed", ex);
            System.Windows.MessageBox.Show($"Window error: {ex}", "WorkTimeTracking", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void HideMainWindow()
    {
        var window = MainWindow;
        if (window != null)
        {
            window.ShowInTaskbar = false;
            window.Hide();
        }
    }

    private MainWindow EnsureMainWindow()
    {
        if (_mainWindow != null)
            return _mainWindow;

        _mainWindow = _services.GetRequiredService<MainWindow>();
        MainWindow = _mainWindow;
        _mainWindow.Closing += OnMainWindowClosing;
        return _mainWindow;
    }

    private void OnMainWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_isExiting) return;

        var minimizeToTray = _settingsService?.Settings.MinimizeToTray ?? true;
        if (minimizeToTray)
        {
            e.Cancel = true;
            HideMainWindow();
        }
    }

    private void RealShutdown()
    {
        _isExiting = true;
        _trayIcon?.Dispose();
        _trayIcon = null;
        Shutdown();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _trackingService?.Stop();
        _gitBranchTrackingService?.Stop();
        _powerEventService?.Dispose();
        (_trackingService as IDisposable)?.Dispose();
        _gitBranchTrackingService?.Dispose();
        _displayStateService?.Dispose();
        _trayIcon?.Dispose();
        if (_localizationService != null)
            _localizationService.LanguageChanged -= UpdateLocalizedUi;

        base.OnExit(e);
    }

    public static T GetRequiredService<T>() where T : notnull =>
        _services.GetRequiredService<T>();

    private static void LogStartup(string message, Exception? ex = null)
    {
        try
        {
            var folder = Path.GetDirectoryName(StartupLogPath);
            if (!string.IsNullOrEmpty(folder))
                Directory.CreateDirectory(folder);

            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {message}";
            if (ex != null)
                line += Environment.NewLine + ex + Environment.NewLine;

            File.AppendAllText(StartupLogPath, line + Environment.NewLine);
        }
        catch
        {
            // ignore logging errors
        }
    }
}
