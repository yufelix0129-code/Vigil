using System.Windows;
using VigilWin.Core;
using VigilWin.Views;

namespace VigilWin.Services;

public sealed class DynamicIslandService
{
    private readonly LogService? _logService;
    private DynamicIslandWindow? _window;

    public DynamicIslandService(LogService? logService = null)
    {
        _logService = logService;
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

    public void ShowSessionStarted(string goal)
    {
        Run(window => window.ShowSessionStarted(goal), "Dynamic Island shown for session start.");
    }

    public void UpdateStatus(FocusStatus status, TimeSpan elapsed, string goal, string? reason = null)
    {
        Run(window => window.UpdateStatus(status, elapsed, goal, reason), null);
    }

    public void ShowDistracted(string goal, string reason, TimeSpan elapsed)
    {
        Run(window => window.ShowDistracted(goal, reason, elapsed), "Dynamic Island expanded for distracted status.");
    }

    public void ShowCompleted(TimeSpan elapsed, int distractionCount)
    {
        Run(window => window.ShowCompleted(elapsed, distractionCount), "Dynamic Island showed completed state.");
    }

    public void ShowStopped(TimeSpan elapsed, string message)
    {
        Run(window => window.ShowStopped(elapsed, message), "Dynamic Island showed stopped state.");
    }

    public void Hide()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() =>
        {
            _window?.SetMode(DynamicIslandMode.Hidden);
            _logService?.Info("Dynamic Island hidden.");
        });
    }

    public void Close()
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() =>
        {
            if (_window is null)
            {
                return;
            }

            _window.Close();
            _window = null;
            _logService?.Info("Dynamic Island closed.");
        });
    }

    private void Run(Action<DynamicIslandWindow> action, string? logMessage)
    {
        if (!IsEnabled)
        {
            return;
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        dispatcher?.BeginInvoke(() =>
        {
            var window = EnsureWindow();
            action(window);

            if (!string.IsNullOrWhiteSpace(logMessage))
            {
                _logService?.Info(logMessage);
            }
        });
    }

    private DynamicIslandWindow EnsureWindow()
    {
        if (_window is not null)
        {
            return _window;
        }

        _window = new DynamicIslandWindow();
        _window.Closed += (_, _) => _window = null;
        return _window;
    }
}
