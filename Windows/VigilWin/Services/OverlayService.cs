using System.Windows;
using VigilWin.Views;

namespace VigilWin.Services;

public sealed class OverlayService
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);
    private DateTime _lastShownUtc = DateTime.MinValue;
    private OverlayWindow? _currentOverlay;

    public void ShowDistractedOverlay(string goal, string reason)
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
            if (_currentOverlay is { IsVisible: true })
            {
                return;
            }

            _currentOverlay = new OverlayWindow(goal, reason);
            _currentOverlay.Closed += (_, _) => _currentOverlay = null;
            _currentOverlay.Show();
        });
    }
}
