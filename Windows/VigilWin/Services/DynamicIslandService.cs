using System.Windows.Threading;
using VigilWin.Core;
using VigilWin.Views;

namespace VigilWin.Services;

public sealed class DynamicIslandService : IDisposable
{
    private readonly LogService? _logService;
    private readonly DispatcherTimer _autoHideTimer = new();
    private readonly List<TimelineSegment> _timelineSegments = [];
    private DynamicIslandWindow? _window;
    private DynamicIslandMode _currentMode = DynamicIslandMode.Hidden;
    private DynamicIslandMode? _scheduledMode;
    private bool _sessionActive;
    private bool _isClosing;
    private bool _isDisposed;
    private bool _suppressedUntilNextSession;

    public DynamicIslandService(LogService? logService = null)
    {
        _logService = logService;
        _autoHideTimer.Tick += AutoHideTimer_Tick;
    }

    public bool IsEnabled { get; private set; } = true;

    public void SetEnabled(bool enabled)
    {
        IsEnabled = enabled;
        if (!enabled)
        {
            Hide();
        }
    }

    public void ShowSessionStarted(string goal, TimeSpan plannedDuration)
    {
        RunOnUi(() =>
        {
            if (!CanShow())
            {
                return;
            }

            _autoHideTimer.Stop();
            _sessionActive = true;
            _isClosing = false;
            _suppressedUntilNextSession = false;
            ResetTimeline();
            var window = EnsureWindow();
            window.ResetForNewSession();
            window.ShowSessionStarted(goal, plannedDuration, GetTimelineSnapshot());
            _currentMode = DynamicIslandMode.Expanded;
            Schedule(DynamicIslandMode.Compact, TimeSpan.FromMilliseconds(1800));
            _logService?.Info("Dynamic Island shown for session start.");
        });
    }

    public void UpdateStatus(FocusStatus status, TimeSpan elapsed, TimeSpan plannedDuration, string goal, string? reason = null)
    {
        RunOnUi(() =>
        {
            if (!CanUpdateStatus())
            {
                return;
            }

            RecordTimelineStatus(status, elapsed);
            var window = EnsureWindow();
            window.UpdateStatus(status, elapsed, plannedDuration, goal, reason, GetTimelineSnapshot());

            if (_currentMode == DynamicIslandMode.Hidden)
            {
                window.TransitionTo(DynamicIslandMode.Compact, force: true);
                _currentMode = DynamicIslandMode.Compact;
            }

            if (status == FocusStatus.Focused && _currentMode == DynamicIslandMode.Alert)
            {
                Schedule(DynamicIslandMode.Compact, TimeSpan.FromMilliseconds(900));
            }
        });
    }

    public void ShowDistracted(string goal, string reason, TimeSpan elapsed, TimeSpan plannedDuration)
    {
        RunOnUi(() =>
        {
            if (!CanShow() || !_sessionActive)
            {
                return;
            }

            _suppressedUntilNextSession = false;
            _isClosing = false;
            RecordTimelineStatus(FocusStatus.Distracted, elapsed);
            var window = EnsureWindow();
            window.ShowDistracted(goal, reason, elapsed, plannedDuration, GetTimelineSnapshot());
            _currentMode = DynamicIslandMode.Alert;
            Schedule(DynamicIslandMode.Compact, TimeSpan.FromSeconds(10));
            _logService?.Info("Dynamic Island expanded for distracted status.");
        });
    }

    public void ShowCompleted(string goal, TimeSpan elapsed, TimeSpan plannedDuration, int distractionCount)
    {
        RunOnUi(() =>
        {
            if (!CanShow())
            {
                return;
            }

            EnterTerminalMode();
            ExtendCurrentTimelineSegment(elapsed);
            var window = EnsureWindow();
            window.ShowCompleted(goal, elapsed, plannedDuration, distractionCount, GetTimelineSnapshot());
            _currentMode = DynamicIslandMode.Completed;
            Schedule(DynamicIslandMode.Hidden, TimeSpan.FromMilliseconds(2500));
            _logService?.Info("Dynamic Island showed completed state.");
        });
    }

    public void ShowStopped(string goal, TimeSpan elapsed, TimeSpan plannedDuration, string message)
    {
        RunOnUi(() =>
        {
            if (!CanShow())
            {
                return;
            }

            EnterTerminalMode();
            ExtendCurrentTimelineSegment(elapsed);
            var window = EnsureWindow();
            window.ShowStopped(goal, elapsed, plannedDuration, message, GetTimelineSnapshot());
            _currentMode = DynamicIslandMode.Completed;
            Schedule(DynamicIslandMode.Hidden, TimeSpan.FromMilliseconds(2500));
            _logService?.Info("Dynamic Island showed stopped state.");
        });
    }

    public void Hide()
    {
        RunOnUi(() =>
        {
            _suppressedUntilNextSession = _sessionActive;
            BeginFadeOutAndClose("Dynamic Island hidden.");
        });
    }

    public void Close()
    {
        RunOnUi(() =>
        {
            _autoHideTimer.Stop();
            _sessionActive = false;
            _isClosing = true;
            _currentMode = DynamicIslandMode.Hidden;

            if (_window is null)
            {
                return;
            }

            var window = _window;
            _window = null;
            window.HideRequested -= Window_HideRequested;
            window.Close();
            _logService?.Info("Dynamic Island closed.");
        });
    }

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        Close();
        _isDisposed = true;
        _autoHideTimer.Tick -= AutoHideTimer_Tick;
    }

    private bool CanShow()
    {
        return IsEnabled && !_isDisposed;
    }

    private bool CanUpdateStatus()
    {
        return CanShow()
            && _sessionActive
            && !_isClosing
            && !_suppressedUntilNextSession
            && _currentMode is not DynamicIslandMode.Completed;
    }

    private void EnterTerminalMode()
    {
        _autoHideTimer.Stop();
        _sessionActive = false;
        _isClosing = false;
        _suppressedUntilNextSession = false;
    }

    private void ResetTimeline()
    {
        _timelineSegments.Clear();
        _timelineSegments.Add(new TimelineSegment
        {
            Status = FocusStatus.Unknown,
            Start = TimeSpan.Zero,
            Duration = TimeSpan.Zero
        });
    }

    private void RecordTimelineStatus(FocusStatus status, TimeSpan elapsed)
    {
        var safeElapsed = elapsed < TimeSpan.Zero ? TimeSpan.Zero : elapsed;
        if (_timelineSegments.Count == 0)
        {
            _timelineSegments.Add(new TimelineSegment
            {
                Status = status,
                Start = TimeSpan.Zero,
                Duration = safeElapsed
            });
            return;
        }

        var current = _timelineSegments[^1];
        if (current.Status == status)
        {
            current.Duration = Max(TimeSpan.Zero, safeElapsed - current.Start);
            return;
        }

        current.Duration = Max(TimeSpan.Zero, safeElapsed - current.Start);
        _timelineSegments.Add(new TimelineSegment
        {
            Status = status,
            Start = safeElapsed,
            Duration = TimeSpan.Zero
        });
    }

    private void ExtendCurrentTimelineSegment(TimeSpan elapsed)
    {
        if (_timelineSegments.Count == 0)
        {
            return;
        }

        var current = _timelineSegments[^1];
        current.Duration = Max(TimeSpan.Zero, elapsed - current.Start);
    }

    private IReadOnlyList<TimelineSegment> GetTimelineSnapshot()
    {
        return _timelineSegments
            .Select(segment => new TimelineSegment
            {
                Status = segment.Status,
                Start = segment.Start,
                Duration = segment.Duration
            })
            .ToList();
    }

    private static TimeSpan Max(TimeSpan first, TimeSpan second)
    {
        return first >= second ? first : second;
    }

    private void Schedule(DynamicIslandMode mode, TimeSpan delay)
    {
        _autoHideTimer.Stop();
        _scheduledMode = mode;
        _autoHideTimer.Interval = delay;
        _autoHideTimer.Start();
    }

    private void AutoHideTimer_Tick(object? sender, EventArgs e)
    {
        _autoHideTimer.Stop();

        if (_scheduledMode is null)
        {
            return;
        }

        var mode = _scheduledMode.Value;
        _scheduledMode = null;

        if (mode == DynamicIslandMode.Hidden)
        {
            BeginFadeOutAndClose("Dynamic Island auto hidden.");
            return;
        }

        if (_window is null || !_sessionActive || _isClosing)
        {
            return;
        }

        _window.TransitionTo(mode);
        _currentMode = mode;
    }

    private void BeginFadeOutAndClose(string logMessage)
    {
        _autoHideTimer.Stop();
        _scheduledMode = null;
        _isClosing = true;
        _sessionActive = false;
        _currentMode = DynamicIslandMode.Hidden;

        if (_window is null)
        {
            _isClosing = false;
            return;
        }

        _window.FadeOutAndClose();
        _logService?.Info(logMessage);
    }

    private void RunOnUi(Action action)
    {
        if (_isDisposed)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        if (dispatcher.CheckAccess())
        {
            action();
            return;
        }

        dispatcher.BeginInvoke(action);
    }

    private DynamicIslandWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = new DynamicIslandWindow();
        _window.HideRequested += Window_HideRequested;
        _window.Closed += Window_Closed;
        return _window;
    }

    private void Window_HideRequested(object? sender, EventArgs e)
    {
        Hide();
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        if (_window is not null)
        {
            _window.HideRequested -= Window_HideRequested;
            _window.Closed -= Window_Closed;
        }

        _window = null;
        _currentMode = DynamicIslandMode.Hidden;
        _isClosing = false;
    }
}
