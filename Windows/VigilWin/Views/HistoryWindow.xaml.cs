using System.Windows;
using System.Windows.Controls;
using VigilWin.Models;
using VigilWin.Services;
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
            var sessions = await _storageService.GetRecentSessionsAsync(20);
            SessionListBox.ItemsSource = sessions;
            DetailGoalText.Text = sessions.Count == 0 ? "暂无历史记录" : "请选择一条记录";
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
        var endTimeText = session.EndTime.HasValue ? session.EndTime.Value.ToString("g") : "未结束";
        DetailStatsText.Text =
            $"开始：{session.StartTime:g}\n" +
            $"结束：{endTimeText}\n" +
            $"计划时长：{TimeSpan.FromSeconds(session.PlannedDurationSeconds):hh\\:mm\\:ss}\n" +
            $"时长：{actualDuration:0.0} 分钟\n" +
            $"Focused：{session.FocusedSeconds} 秒；Wandering：{session.WanderingSeconds} 秒；Distracted：{session.DistractedSeconds} 秒；Idle：{session.IdleSeconds} 秒\n" +
            $"分心次数：{session.DistractionCount}";
        DetailSummaryText.Text = string.IsNullOrWhiteSpace(session.Summary)
            ? "暂无总结。"
            : session.Summary;

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
