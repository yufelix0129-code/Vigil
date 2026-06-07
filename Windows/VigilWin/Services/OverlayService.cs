using System.Windows;
using VigilWin.Views;

namespace VigilWin.Services;

public sealed class OverlayService
{
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(20);
    private readonly LogService? _logService;
    private DateTime _lastShownUtc = DateTime.MinValue;
    private OverlayWindow? _currentOverlay;

    public OverlayService(LogService? logService = null)
    {
        _logService = logService;
    }

    public void ShowDistractedOverlay(string goal, string reason)
    {
        if (DateTime.UtcNow - _lastShownUtc < Cooldown)
        {
            _logService?.Info("Overlay suppressed by cooldown.");
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
                _logService?.Info("Overlay already visible; skipped creating another overlay.");
                return;
            }

            _currentOverlay = new OverlayWindow(goal, reason);
            _currentOverlay.Closed += (_, _) =>
            {
                _currentOverlay = null;
                _logService?.Info("Overlay closed.");
            };
            _currentOverlay.Show();
            _logService?.Info("Overlay opened.");
        });
    }
}
