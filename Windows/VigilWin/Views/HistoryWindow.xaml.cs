using System.Windows;
using System.Windows.Controls;
using VigilWin.Models;
using VigilWin.Services;
using VigilWin.Utilities;
using MessageBox = System.Windows.MessageBox;

namespace VigilWin.Views;

public partial class HistoryWindow : Window
{
    private readonly StorageService _storageService;
    private readonly LogService? _logService;

    public HistoryWindow(StorageService storageService, LogService? logService = null)
    {
        InitializeComponent();
        _storageService = storageService;
        _logService = logService;
        Loaded += HistoryWindow_Loaded;
    }

    private async void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            AnimationHelper.FadeInUp(HistoryShell, durationMs: 300);
            AnimationHelper.FadeInUp(RecentSessionsCard, fromY: 10, durationMs: 280, delayMs: 50);
            AnimationHelper.FadeInUp(SessionDetailsCard, fromY: 10, durationMs: 280, delayMs: 100);
            var sessions = await _storageService.GetRecentSessionsAsync(20);
            SessionListBox.ItemsSource = sessions;
            SessionListBox.Visibility = sessions.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            EmptySessionsText.Visibility = sessions.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
            EmptyDetailsText.Text = sessions.Count == 0
                ? "Start a focus session to see details here."
                : "Select a session to see details.";
            EmptyDetailsText.Visibility = Visibility.Visible;
            DetailsContentPanel.Visibility = Visibility.Collapsed;
            _logService?.Info($"History sessions loaded. count={sessions.Count}");
        }
        catch (Exception ex)
        {
            _logService?.Error("History session load failed.", ex);
            MessageBox.Show($"加载历史记录失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void SessionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SessionListBox.SelectedItem is not FocusSession session)
        {
            return;
        }

        DetailGoalText.Text = session.Goal;
        var actualDuration = ((session.EndTime ?? DateTime.Now) - session.StartTime).TotalMinutes;
        var endTimeText = session.EndTime.HasValue ? session.EndTime.Value.ToString("g") : "Not ended";
        DetailStatsText.Text =
            $"Started: {session.StartTime:g}\n" +
            $"Ended: {endTimeText}\n" +
            $"Planned: {TimeSpan.FromSeconds(session.PlannedDurationSeconds):hh\\:mm\\:ss}\n" +
            $"Duration: {actualDuration:0.0} minutes\n" +
            $"Focused: {session.FocusedSeconds}s; Wandering: {session.WanderingSeconds}s; Distracted: {session.DistractedSeconds}s; Idle: {session.IdleSeconds}s\n" +
            $"Distractions: {session.DistractionCount}";
        DetailSummaryText.Text = string.IsNullOrWhiteSpace(session.Summary)
            ? "No summary yet."
            : session.Summary;
        FocusedSecondsText.Text = $"{session.FocusedSeconds}s";
        WanderingSecondsText.Text = $"{session.WanderingSeconds}s";
        DistractedSecondsText.Text = $"{session.DistractedSeconds}s";
        IdleSecondsText.Text = $"{session.IdleSeconds}s";
        EmptyDetailsText.Visibility = Visibility.Collapsed;
        DetailsContentPanel.Visibility = Visibility.Visible;
        AnimationHelper.FadeInUp(DetailsContentPanel, fromY: 6, durationMs: 220);

        try
        {
            FrameRecordsListView.ItemsSource = await _storageService.GetFrameRecordsAsync(session.Id);
            _logService?.Info($"History frame records loaded. sessionId={session.Id}");
        }
        catch (Exception ex)
        {
            _logService?.Error("History frame record load failed.", ex);
            MessageBox.Show($"加载分析记录失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
