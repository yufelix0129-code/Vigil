using System.Windows;
using VigilWin.Views;

namespace VigilWin.Services;

public sealed class NotificationService
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);
    private DateTime _lastShownUtc = DateTime.MinValue;

    public void ShowDistractedNotification(string goal, string reason)
    {
        if (DateTime.UtcNow - _lastShownUtc < Cooldown)
        {
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
        });
    }
}
