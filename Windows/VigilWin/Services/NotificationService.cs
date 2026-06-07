using System.Windows;
using VigilWin.Views;

namespace VigilWin.Services;

public sealed class NotificationService
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);
    private readonly LogService? _logService;
    private DateTime _lastShownUtc = DateTime.MinValue;

    public NotificationService(LogService? logService = null)
    {
        _logService = logService;
    }

    public void ShowDistractedNotification(string goal, string reason)
    {
        if (DateTime.UtcNow - _lastShownUtc < Cooldown)
        {
            _logService?.Info("Distracted notification suppressed by cooldown.");
            return;
        }

        _lastShownUtc = DateTime.UtcNow;

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher is null)
        {
            return;
        }

        dispatcher.BeginInvoke(() =>
        {
            var window = new FloatingReminderWindow(goal, reason);
            window.Show();
            _logService?.Info("Distracted notification shown.");
        });
    }
}
