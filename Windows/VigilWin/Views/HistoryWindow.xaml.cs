using System.Windows;
using System.Windows.Controls;
using VigilWin.Models;
using VigilWin.Services;
using MessageBox = System.Windows.MessageBox;

namespace VigilWin.Views;

public partial class HistoryWindow : Window
{
    private readonly StorageService _storageService;

    public HistoryWindow(StorageService storageService)
    {
        InitializeComponent();
        _storageService = storageService;
        Loaded += HistoryWindow_Loaded;
    }

    private async void HistoryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            SessionListBox.ItemsSource = await _storageService.GetRecentSessionsAsync(20);
        }
        catch (Exception ex)
        {
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
        DetailStatsText.Text =
            $"开始：{session.StartTime:g}\n" +
            $"时长：{actualDuration:0.0} 分钟\n" +
            $"Focused：{session.FocusedSeconds} 秒；Wandering：{session.WanderingSeconds} 秒；Distracted：{session.DistractedSeconds} 秒；Idle：{session.IdleSeconds} 秒\n" +
            $"分心次数：{session.DistractionCount}";
        DetailSummaryText.Text = string.IsNullOrWhiteSpace(session.Summary)
            ? "暂无总结。"
            : session.Summary;

        try
        {
            FrameRecordsListView.ItemsSource = await _storageService.GetFrameRecordsAsync(session.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载分析记录失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
