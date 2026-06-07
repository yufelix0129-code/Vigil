using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using VigilWin.Core;
using VigilWin.Models;
using VigilWin.Services;
using VigilWin.Views;
using MessageBox = System.Windows.MessageBox;

namespace VigilWin;

public partial class MainWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly StorageService _storageService;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly FocusSessionManager _sessionManager;

    public MainWindow()
    {
        InitializeComponent();

        _settingsService = new SettingsService();
        _storageService = new StorageService();
        _screenCaptureService = new ScreenCaptureService();
        var aiService = new AIService();
        var idleDetectorService = new IdleDetectorService();
        var notificationService = new NotificationService();
        var overlayService = new OverlayService();
        var frameAnalyzer = new FrameAnalyzer();

        _sessionManager = new FocusSessionManager(
            _screenCaptureService,
            aiService,
            idleDetectorService,
            _storageService,
            _settingsService,
            notificationService,
            overlayService,
            frameAnalyzer);

        _sessionManager.StateChanged += SessionManager_StateChanged;
        _sessionManager.TickUpdated += SessionManager_TickUpdated;
        _sessionManager.AnalysisUpdated += SessionManager_AnalysisUpdated;
        _sessionManager.SessionCompleted += SessionManager_SessionCompleted;
        _sessionManager.ErrorOccurred += SessionManager_ErrorOccurred;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
    }

    private void FocusGoalTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (FocusGoalPlaceholder is null)
        {
            return;
        }

        FocusGoalPlaceholder.Visibility = string.IsNullOrEmpty(FocusGoalTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private async void StartFocusButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(FocusGoalTextBox.Text))
        {
            MessageBox.Show(
                "请先输入专注目标",
                "Vigil",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            FocusGoalTextBox.Focus();
            return;
        }

        try
        {
            await _sessionManager.StartSessionAsync(FocusGoalTextBox.Text, GetSelectedDuration());
        }
        catch (Exception ex)
        {
            MessageBox.Show($"启动专注失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void StopFocusButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await _sessionManager.StopSessionAsync();
            CurrentStatusText.Text = "当前状态：已停止";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"停止专注失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settingsService)
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var historyWindow = new HistoryWindow(_storageService)
        {
            Owner = this
        };

        historyWindow.ShowDialog();
    }

    private async void TestScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var screenshot = await _screenCaptureService.CapturePrimaryScreenJpegAsync();
            Directory.CreateDirectory(SettingsService.AppDataDirectory);
            var path = Path.Combine(SettingsService.AppDataDirectory, "test-screenshot.jpg");
            await File.WriteAllBytesAsync(path, screenshot);

            MessageBox.Show($"测试截屏已保存：\n{path}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            LatestReasonText.Text = $"AI 判断原因：截屏失败：{ex.Message}";
            MessageBox.Show($"测试截屏失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsService.Load();
            await _storageService.InitializeAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"初始化本地数据失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        await _sessionManager.StopSessionAsync();
    }

    private void SessionManager_StateChanged(object? sender, SessionState state)
    {
        Dispatcher.BeginInvoke(() =>
        {
            CurrentStatusText.Text = $"当前状态：{FormatState(state)}";
        });
    }

    private void SessionManager_TickUpdated(object? sender, FocusSession session)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var elapsed = (session.EndTime ?? DateTime.Now) - session.StartTime;
            ElapsedTimeText.Text = $"已专注时间：{FormatElapsed(elapsed)}";
            DistractionCountText.Text = $"分心次数：{session.DistractionCount}";
        });
    }

    private void SessionManager_AnalysisUpdated(object? sender, FrameRecord record)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LatestReasonText.Text = $"AI 判断原因：[{record.Status}] {record.Reason}";
        });
    }

    private void SessionManager_SessionCompleted(object? sender, FocusSession session)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LatestReasonText.Text = string.IsNullOrWhiteSpace(session.Summary)
                ? "AI 判断原因：会话已结束。"
                : $"AI 判断原因：会话已结束。{session.Summary}";
        });
    }

    private void SessionManager_ErrorOccurred(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            LatestReasonText.Text = $"AI 判断原因：{message}";
        });
    }

    private TimeSpan GetSelectedDuration()
    {
        if (DurationComboBox.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content?.ToString() ?? string.Empty;
            var firstPart = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (int.TryParse(firstPart, out var minutes))
            {
                return TimeSpan.FromMinutes(minutes);
            }
        }

        return TimeSpan.FromMinutes(25);
    }

    private static string FormatElapsed(TimeSpan elapsed)
    {
        return elapsed.TotalHours >= 1
            ? elapsed.ToString(@"hh\:mm\:ss")
            : elapsed.ToString(@"mm\:ss");
    }

    private static string FormatState(SessionState state)
    {
        return state switch
        {
            SessionState.Idle => "未开始",
            SessionState.Preparing => "准备中",
            SessionState.Running => "运行中",
            SessionState.Analyzing => "分析中",
            SessionState.Completed => "已完成",
            SessionState.Cancelled => "已停止",
            SessionState.Error => "错误",
            _ => state.ToString()
        };
    }
}
