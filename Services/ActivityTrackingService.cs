using System.Windows.Threading;
using WorkTimeTracking.Models;

namespace WorkTimeTracking.Services;

public class ActivityTrackingService : IDisposable
{
    private readonly InputHookService _inputHookService;
    private readonly SessionRepository _sessionRepository;
    private readonly ISettingsService _settingsService;
    private readonly DisplayStateService _displayStateService;
    private readonly DispatcherTimer _inactivityTimer;
    private readonly DispatcherTimer _screenOffTimer;
    private readonly DispatcherTimer _wakeValidationTimer;

    private const int WakeValidationBufferMinutes = 2;

    private DateTime _lastInputTime;
    private Session? _currentSession;
    private bool _isTracking;
    private bool _isInactive;
    private SessionReason _inactiveReason;
    private DateTime? _pendingScreenOffTime;

    // Wake validation: prevent false Activity sessions from random PC wake-ups
    private bool _postWakeValidation;
    private DateTime? _firstInputAfterWake;

    // Task session tracking
    private TaskSession? _currentTaskSession;
    private DateTime? _sleepStartTime;

    public event Action? StateChanged;

    public bool IsTracking => _isTracking;
    public Session? CurrentSession => _currentSession;
    public TaskSession? CurrentTaskSession => _currentTaskSession;
    public bool HasActiveTaskSession => _currentTaskSession != null;

    public ActivityTrackingService(
        InputHookService inputHookService,
        SessionRepository sessionRepository,
        ISettingsService settingsService,
        DisplayStateService displayStateService)
    {
        _inputHookService = inputHookService;
        _sessionRepository = sessionRepository;
        _settingsService = settingsService;
        _displayStateService = displayStateService;

        _inactivityTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _inactivityTimer.Tick += CheckInactivity;

        _screenOffTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _screenOffTimer.Tick += ConfirmScreenOff;

        _wakeValidationTimer = new DispatcherTimer();
        _wakeValidationTimer.Tick += OnWakeValidationComplete;

        _inputHookService.InputDetected += OnInputDetected;
        _displayStateService.DisplayStateChanged += OnDisplayStateChanged;
        _settingsService.SettingsChanged += OnSettingsChanged;
    }

    public void Start()
    {
        if (_isTracking) return;

        _isTracking = true;
        _lastInputTime = DateTime.Now;
        _isInactive = false;
        _inactiveReason = SessionReason.None;

        // Close any orphaned open sessions from previous runs
        _sessionRepository.CloseAllOpenSessions(DateTime.Now);

        _inputHookService.Start();
        UpdateInactivityTimers();

        StartNewSession(SessionType.Activity);
        StateChanged?.Invoke();
    }

    public void Stop()
    {
        if (!_isTracking) return;

        _isTracking = false;
        _inactivityTimer.Stop();
        _screenOffTimer.Stop();
        _inputHookService.Stop();

        // Close task session if active
        if (_currentTaskSession != null)
            StopTaskSession();

        CloseCurrentSession();
        StateChanged?.Invoke();
    }

    // --- Task Session management ---

    public void StartTaskSession()
    {
        if (_currentTaskSession != null) return;

        _currentTaskSession = new TaskSession
        {
            StartTime = DateTime.Now
        };
        _sessionRepository.AddTaskSession(_currentTaskSession);
        StateChanged?.Invoke();
    }

    public TaskSession? StopTaskSession()
    {
        if (_currentTaskSession == null) return null;

        var now = DateTime.Now;
        _sessionRepository.CloseTaskSession(_currentTaskSession.Id, now);

        // Return a snapshot with final values
        var finished = new TaskSession
        {
            Id = _currentTaskSession.Id,
            StartTime = _currentTaskSession.StartTime,
            EndTime = now,
            AwayTime = _currentTaskSession.AwayTime
        };

        _currentTaskSession = null;
        StateChanged?.Invoke();
        return finished;
    }

    // --- Sleep / Wake ---

    public void OnSleep()
    {
        if (!_isTracking) return;

        // Cancel wake validation if in progress — PC going back to sleep
        _postWakeValidation = false;
        _wakeValidationTimer.Stop();
        _firstInputAfterWake = null;

        _sleepStartTime = DateTime.Now;
        CloseCurrentSession();
        StartNewSession(SessionType.Sleep, SessionReason.Sleep);
        StateChanged?.Invoke();
    }

    public void OnWake()
    {
        if (!_isTracking) return;

        // Calculate away duration and add to active task session
        if (_sleepStartTime.HasValue && _currentTaskSession != null)
        {
            var awayDuration = DateTime.Now - _sleepStartTime.Value;
            _currentTaskSession.AwayTime += awayDuration;
            _sessionRepository.AddAwayTimeToTaskSession(_currentTaskSession.Id, awayDuration);
        }
        _sleepStartTime = null;

        // Close sleep session but DON'T start Activity yet.
        // Stay in Inactivity until wake validation confirms real user presence.
        CloseCurrentSession();
        StartNewSession(SessionType.Inactivity, SessionReason.Sleep);
        _isInactive = true;
        _inactiveReason = SessionReason.Sleep;
        _postWakeValidation = true;
        _firstInputAfterWake = null;

        // Start validation timer: InactivityTimeoutMinutes + buffer
        var validationMinutes = _settingsService.Settings.InactivityTimeoutMinutes + WakeValidationBufferMinutes;
        _wakeValidationTimer.Interval = TimeSpan.FromMinutes(validationMinutes);
        _wakeValidationTimer.Start();

        StateChanged?.Invoke();
    }

    // --- Input / Inactivity ---

    private void OnInputDetected()
    {
        _lastInputTime = DateTime.Now;

        if (_pendingScreenOffTime.HasValue)
        {
            _pendingScreenOffTime = null;
            _screenOffTimer.Stop();
        }

        if (_isInactive && _isTracking)
        {
            if (_postWakeValidation)
            {
                // Record first input time but don't resume yet — waiting for validation
                _firstInputAfterWake ??= DateTime.Now;
            }
            else
            {
                ResumeFromInactivity();
            }
        }
    }

    private void OnSettingsChanged()
    {
        if (_isTracking)
            UpdateInactivityTimers();
    }

    private void UpdateInactivityTimers()
    {
        if (_settingsService.Settings.InactivityMode == InactivityDetectionMode.TimeoutMinutes)
            _inactivityTimer.Start();
        else
            _inactivityTimer.Stop();
    }

    private void CheckInactivity(object? sender, EventArgs e)
    {
        if (!_isTracking || _isInactive) return;
        if (_settingsService.Settings.InactivityMode != InactivityDetectionMode.TimeoutMinutes) return;

        var threshold = TimeSpan.FromMinutes(_settingsService.Settings.InactivityTimeoutMinutes);
        var idleTime = DateTime.Now - _lastInputTime;

        if (idleTime >= threshold)
        {
            _isInactive = true;
            _inactiveReason = SessionReason.KeyboardMouseIdle;
            CloseCurrentSession();
            StartNewSession(SessionType.Inactivity, SessionReason.KeyboardMouseIdle);
            StateChanged?.Invoke();
        }
    }

    // --- Session helpers ---

    private void StartNewSession(SessionType type, SessionReason reason = SessionReason.None, DateTime? startTime = null)
    {
        _currentSession = new Session
        {
            StartTime = startTime ?? DateTime.Now,
            Type = type,
            Reason = reason
        };
        _sessionRepository.AddSession(_currentSession);
    }

    private void CloseCurrentSession()
    {
        CloseCurrentSession(DateTime.Now);
    }

    private void CloseCurrentSession(DateTime endTime)
    {
        if (_currentSession?.Id > 0)
        {
            _sessionRepository.CloseSession(_currentSession.Id, endTime);
            _currentSession = null;
        }
    }

    public List<Session> GetTodaySessions() =>
        _sessionRepository.GetSessionsForDate(DateTime.Today);

    public List<TaskSession> GetTodayTaskSessions() =>
        _sessionRepository.GetTaskSessionsForDate(DateTime.Today);

    public void Dispose()
    {
        Stop();
        _inactivityTimer.Tick -= CheckInactivity;
        _screenOffTimer.Tick -= ConfirmScreenOff;
        _wakeValidationTimer.Tick -= OnWakeValidationComplete;
        _inputHookService.InputDetected -= OnInputDetected;
        _displayStateService.DisplayStateChanged -= OnDisplayStateChanged;
        _settingsService.SettingsChanged -= OnSettingsChanged;
        GC.SuppressFinalize(this);
    }

    private void OnDisplayStateChanged(DisplayState state)
    {
        if (!_isTracking) return;
        if (_settingsService.Settings.InactivityMode != InactivityDetectionMode.ScreenOff) return;

        if (state == DisplayState.Off)
        {
            _pendingScreenOffTime = DateTime.Now;
            _screenOffTimer.Stop();
            _screenOffTimer.Start();
        }
        else if (state == DisplayState.On)
        {
            _pendingScreenOffTime = null;
            _screenOffTimer.Stop();

            if (_isInactive && _inactiveReason == SessionReason.ScreenOff)
                ResumeFromInactivity();
        }
    }

    private void ConfirmScreenOff(object? sender, EventArgs e)
    {
        _screenOffTimer.Stop();
        if (!_pendingScreenOffTime.HasValue) return;

        var screenOffTime = _pendingScreenOffTime.Value;
        _pendingScreenOffTime = null;

        if (_lastInputTime > screenOffTime)
            return;

        if (_isInactive) return;

        var timeoutSeconds = _displayStateService.GetDisplayTimeoutSeconds();
        var awayStart = timeoutSeconds > 0
            ? screenOffTime - TimeSpan.FromSeconds(timeoutSeconds)
            : screenOffTime;

        if (_currentSession != null && awayStart < _currentSession.StartTime)
            awayStart = _currentSession.StartTime;

        _isInactive = true;
        _inactiveReason = SessionReason.ScreenOff;

        CloseCurrentSession(awayStart);
        _lastInputTime = awayStart;
        StartNewSession(SessionType.Inactivity, SessionReason.ScreenOff, awayStart);
        StateChanged?.Invoke();
    }

    private void ResumeFromInactivity()
    {
        _isInactive = false;
        _inactiveReason = SessionReason.None;
        CloseCurrentSession();
        StartNewSession(SessionType.Activity);
        StateChanged?.Invoke();
    }

    private void OnWakeValidationComplete(object? sender, EventArgs e)
    {
        _wakeValidationTimer.Stop();
        _postWakeValidation = false;

        if (_firstInputAfterWake.HasValue)
        {
            var idleTime = DateTime.Now - _lastInputTime;
            var threshold = TimeSpan.FromMinutes(_settingsService.Settings.InactivityTimeoutMinutes);

            if (idleTime < threshold)
            {
                // User is still actively providing input — confirmed real presence.
                // Start Activity retroactively from first input after wake.
                CloseCurrentSession(_firstInputAfterWake.Value);
                StartNewSession(SessionType.Activity, SessionReason.None, _firstInputAfterWake.Value);
                _isInactive = false;
                _inactiveReason = SessionReason.None;
                StateChanged?.Invoke();
            }
            // else: user provided some input but went idle — stay in inactivity
        }
        // No input detected at all — false wake confirmed, stay in inactivity

        _firstInputAfterWake = null;
    }
}
