using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using VigilWin.Core;
using VigilWin.Models;
using VigilWin.Services;
using VigilWin.Views;
using MessageBox = System.Windows.MessageBox;

namespace VigilWin;

public partial class MainWindow : Window
{
    private static readonly System.Windows.Media.Brush NeutralStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(166, 175, 189));
    private static readonly System.Windows.Media.Brush RunningStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(35, 169, 130));
    private static readonly System.Windows.Media.Brush AnalyzingStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(109, 106, 242));
    private static readonly System.Windows.Media.Brush WarningStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 158, 11));
    private static readonly System.Windows.Media.Brush DangerStatusBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 98, 98));

    private readonly SettingsService _settingsService;
    private readonly StorageService _storageService;
    private readonly ScreenCaptureService _screenCaptureService;
    private readonly FocusSessionManager _sessionManager;
    private readonly DynamicIslandService _dynamicIslandService;
    private readonly LogService _logService;
    private readonly DispatcherTimer _uiTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1)
    };

    private DateTime? _activeSessionStartTime;
    private TimeSpan _lastElapsed = TimeSpan.Zero;
    private TimeSpan _plannedDuration = TimeSpan.Zero;
    private string _activeGoal = string.Empty;
    private FocusStatus _latestFocusStatus = FocusStatus.Unknown;
    private string _latestReason = string.Empty;
    private int _latestDistractionCount;
    private bool _isClosing;

    public MainWindow()
    {
        InitializeComponent();

        _logService = new LogService();
        _logService.Info("App starting.");
        var secureSecretService = new SecureSecretService();
        _settingsService = new SettingsService(secureSecretService, _logService);
        _storageService = new StorageService(_logService);
        _screenCaptureService = new ScreenCaptureService(_logService);
        var aiService = new AIService(_logService);
        var idleDetectorService = new IdleDetectorService();
        var notificationService = new NotificationService(_logService);
        var overlayService = new OverlayService(_logService);
        var frameAnalyzer = new FrameAnalyzer();
        _dynamicIslandService = new DynamicIslandService(_logService);

        _sessionManager = new FocusSessionManager(
            _screenCaptureService,
            aiService,
            idleDetectorService,
            _storageService,
            _settingsService,
            notificationService,
            overlayService,
            frameAnalyzer,
            _logService);

        _sessionManager.StateChanged += SessionManager_StateChanged;
        _sessionManager.TickUpdated += SessionManager_TickUpdated;
        _sessionManager.AnalysisUpdated += SessionManager_AnalysisUpdated;
        _sessionManager.SessionCompleted += SessionManager_SessionCompleted;
        _sessionManager.ErrorOccurred += SessionManager_ErrorOccurred;
        _uiTimer.Tick += UiTimer_Tick;

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
            var duration = GetSelectedDuration();
            await _sessionManager.StartSessionAsync(FocusGoalTextBox.Text, duration);
            StartSessionUi(FocusGoalTextBox.Text.Trim(), duration);
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
            var hadActiveSession = _activeSessionStartTime is not null;
            await _sessionManager.StopSessionAsync();
            if (!hadActiveSession && _activeSessionStartTime is null)
            {
                CurrentStatusText.Text = "Stopped";
                UpdateStatusDot(SessionState.Cancelled);
                _dynamicIslandService.Hide();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"停止专注失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new SettingsWindow(_settingsService, _logService)
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
        ApplyDynamicIslandSetting();
    }

    private void HistoryButton_Click(object sender, RoutedEventArgs e)
    {
        var historyWindow = new HistoryWindow(_storageService, _logService)
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
            _logService.Info($"Test screenshot saved. path={path}");

            MessageBox.Show($"测试截屏已保存：\n{path}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            _logService.Error("Test screenshot failed.", ex);
            LatestReasonText.Text = $"AI 判断原因：截屏失败：{ex.Message}";
            MessageBox.Show($"测试截屏失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _settingsService.Load();
            ApplyDynamicIslandSetting();
            await _storageService.InitializeAsync();
            _logService.Info("MainWindow loaded.");
        }
        catch (Exception ex)
        {
            _logService.Error("App initialization failed.", ex);
            MessageBox.Show($"初始化本地数据失败：{ex.Message}", "Vigil", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        _isClosing = true;
        _uiTimer.Stop();
        _dynamicIslandService.SetEnabled(false);
        _dynamicIslandService.Close();
        await _sessionManager.StopSessionAsync();
    }

    private void SessionManager_StateChanged(object? sender, SessionState state)
    {
        Dispatcher.BeginInvoke(() =>
        {
            CurrentStatusText.Text = FormatState(state);
            UpdateStatusDot(state);

            if (state is SessionState.Error)
            {
                StopUiTimer();
            }
        });
    }

    private void SessionManager_TickUpdated(object? sender, FocusSession session)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _activeGoal = session.Goal;
            _plannedDuration = TimeSpan.FromSeconds(session.PlannedDurationSeconds);
            _latestDistractionCount = session.DistractionCount;
            DistractionCountText.Text = _latestDistractionCount.ToString();

            if (!_uiTimer.IsEnabled)
            {
                _lastElapsed = (session.EndTime ?? DateTime.Now) - session.StartTime;
                ElapsedTimeText.Text = FormatElapsed(_lastElapsed);
            }
        });
    }

    private void SessionManager_AnalysisUpdated(object? sender, FrameRecord record)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _latestFocusStatus = record.Status;
            _latestReason = record.Reason;
            LatestReasonText.Text = $"[{record.Status}] {record.Reason}";
            UpdateStatusDot(record.Status);

            if (record.Status == FocusStatus.Distracted)
            {
                _dynamicIslandService.ShowDistracted(_activeGoal, record.Reason, _lastElapsed, _plannedDuration);
            }
            else
            {
                _dynamicIslandService.UpdateStatus(record.Status, _lastElapsed, _plannedDuration, _activeGoal, record.Reason);
            }
        });
    }

    private void SessionManager_SessionCompleted(object? sender, FocusSession session)
    {
        Dispatcher.BeginInvoke(() =>
        {
            StopUiTimer();
            _lastElapsed = (session.EndTime ?? DateTime.Now) - session.StartTime;
            _plannedDuration = TimeSpan.FromSeconds(session.PlannedDurationSeconds);
            ElapsedTimeText.Text = FormatElapsed(_lastElapsed);
            _latestDistractionCount = session.DistractionCount;
            DistractionCountText.Text = _latestDistractionCount.ToString();

            LatestReasonText.Text = string.IsNullOrWhiteSpace(session.Summary)
                ? "Session completed."
                : $"Session completed. {session.Summary}";

            var finalState = _sessionManager.CurrentState;
            if (finalState == SessionState.Cancelled)
            {
                _dynamicIslandService.ShowStopped(session.Goal, _lastElapsed, _plannedDuration, "Session stopped.");
            }
            else if (finalState == SessionState.Error)
            {
                _dynamicIslandService.ShowStopped(session.Goal, _lastElapsed, _plannedDuration, "Session ended with an error.");
            }
            else
            {
                _dynamicIslandService.ShowCompleted(session.Goal, _lastElapsed, _plannedDuration, session.DistractionCount);
            }

            _activeSessionStartTime = null;
        });
    }

    private void SessionManager_ErrorOccurred(object? sender, string message)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _latestReason = message;
            LatestReasonText.Text = message;
            _dynamicIslandService.UpdateStatus(_latestFocusStatus, _lastElapsed, _plannedDuration, _activeGoal, message);
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
        return ElapsedTimeFormatter.Format(elapsed);
    }

    private static string FormatState(SessionState state)
    {
        return state switch
        {
            SessionState.Idle => "Not started",
            SessionState.Preparing => "Preparing",
            SessionState.Running => "Running",
            SessionState.Analyzing => "Analyzing",
            SessionState.Completed => "Completed",
            SessionState.Cancelled => "Stopped",
            SessionState.Error => "Error",
            _ => state.ToString()
        };
    }

    private void UpdateStatusDot(SessionState state)
    {
        StatusDot.Fill = state switch
        {
            SessionState.Running => RunningStatusBrush,
            SessionState.Analyzing => AnalyzingStatusBrush,
            SessionState.Error => DangerStatusBrush,
            _ => NeutralStatusBrush
        };
    }

    private void UpdateStatusDot(FocusStatus status)
    {
        StatusDot.Fill = status switch
        {
            FocusStatus.Focused => RunningStatusBrush,
            FocusStatus.Wandering => WarningStatusBrush,
            FocusStatus.Distracted => DangerStatusBrush,
            FocusStatus.Idle => NeutralStatusBrush,
            _ => AnalyzingStatusBrush
        };
    }

    private void StartSessionUi(string goal, TimeSpan plannedDuration)
    {
        _activeSessionStartTime = _sessionManager.CurrentSessionStartTime ?? DateTime.Now;
        _lastElapsed = TimeSpan.Zero;
        _plannedDuration = plannedDuration;
        _activeGoal = goal;
        _latestFocusStatus = FocusStatus.Unknown;
        _latestReason = "Focus session started.";
        _latestDistractionCount = 0;
        ElapsedTimeText.Text = FormatElapsed(_lastElapsed);
        DistractionCountText.Text = "0";
        LatestReasonText.Text = "Focus session started.";

        ApplyDynamicIslandSetting();
        _dynamicIslandService.ShowSessionStarted(goal, _plannedDuration);
        if (!_isClosing)
        {
            _dynamicIslandService.UpdateStatus(_latestFocusStatus, _lastElapsed, _plannedDuration, _activeGoal, _latestReason);
        }

        if (!_uiTimer.IsEnabled)
        {
            _uiTimer.Start();
        }
    }

    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        RefreshElapsedDisplay();
    }

    private void RefreshElapsedDisplay()
    {
        if (_activeSessionStartTime is null)
        {
            return;
        }

        _lastElapsed = DateTime.Now - _activeSessionStartTime.Value;
        ElapsedTimeText.Text = FormatElapsed(_lastElapsed);
        if (!_isClosing)
        {
            _dynamicIslandService.UpdateStatus(_latestFocusStatus, _lastElapsed, _plannedDuration, _activeGoal, _latestReason);
        }
    }

    private void StopUiTimer()
    {
        if (_uiTimer.IsEnabled)
        {
            _uiTimer.Stop();
        }
    }

    private void ApplyDynamicIslandSetting()
    {
        var settings = _settingsService.Load();
        _dynamicIslandService.SetEnabled(settings.EnableDynamicIsland);

        if (settings.EnableDynamicIsland && _activeSessionStartTime is not null)
        {
            _dynamicIslandService.UpdateStatus(_latestFocusStatus, _lastElapsed, _plannedDuration, _activeGoal, _latestReason);
        }
    }
}
