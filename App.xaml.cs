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
    private Mutex? _singleInstanceMutex;
    private EventWaitHandle? _showWindowEvent;
    private RegisteredWaitHandle? _showWindowWait;
    private const string SingleInstanceMutexName = @"Local\WorkTimeTracking.SingleInstance";
    private const string ShowWindowEventName = @"Local\WorkTimeTracking.ShowWindow";
    private static readonly string StartupLogPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WorkTimeTracking", "startup.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        LogStartup("OnStartup begin");

        if (!TryAcquireSingleInstance())
        {
            LogStartup("Another instance is running, exiting");
            Shutdown();
            return;
        }

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

    private bool TryAcquireSingleInstance()
    {
        _singleInstanceMutex = new Mutex(true, SingleInstanceMutexName, out var createdNew);
        if (!createdNew)
        {
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
            try
            {
                using var showEvent = EventWaitHandle.OpenExisting(ShowWindowEventName);
                showEvent.Set();
            }
            catch
            {
                // first instance is still starting up
            }
            return false;
        }

        _showWindowEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowWindowEventName);
        _showWindowWait = ThreadPool.RegisterWaitForSingleObject(
            _showWindowEvent,
            (_, _) => Dispatcher.BeginInvoke(new Action(ShowMainWindow)),
            null,
            Timeout.Infinite,
            executeOnlyOnce: false);
        return true;
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        // Data
        // Disposable transients resolved from the root provider stay referenced until exit,
        // so every query leaked a DbContext. Create contexts directly instead.
        services.AddSingleton<Func<AppDbContext>>(_ => () => new AppDbContext());
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
        services.AddSingleton<TrayIconService>();

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

        _trayIcon = _services.GetRequiredService<TrayIconService>();
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
        (_services as IDisposable)?.Dispose();

        _showWindowWait?.Unregister(null);
        _showWindowEvent?.Dispose();
        if (_singleInstanceMutex != null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
        }

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
