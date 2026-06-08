using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using VigilWin.Core;

namespace VigilWin.Views;

public partial class DynamicIslandWindow : Window
{
    private const double CompactWidth = 300;
    private const double CompactHeight = 76;
    private const double ExpandedWidth = 520;
    private const double ExpandedHeight = 190;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    private readonly DispatcherTimer _modeTimer = new();
    private DynamicIslandMode _mode = DynamicIslandMode.Hidden;
    private FocusStatus _status = FocusStatus.Unknown;

    public DynamicIslandWindow()
    {
        InitializeComponent();
        SourceInitialized += DynamicIslandWindow_SourceInitialized;
        _modeTimer.Tick += ModeTimer_Tick;
    }

    public void ShowSessionStarted(string goal)
    {
        UpdateContent(FocusStatus.Unknown, TimeSpan.Zero, goal, "Vigil is monitoring your focus session.");
        ExpandedTitleText.Text = "Focus session started";
        SetMode(DynamicIslandMode.Expanded);
        ScheduleMode(DynamicIslandMode.Compact, TimeSpan.FromSeconds(2));
    }

    public void UpdateStatus(FocusStatus status, TimeSpan elapsed, string goal, string? reason = null)
    {
        UpdateContent(status, elapsed, goal, reason);

        if (_mode is DynamicIslandMode.Hidden)
        {
            SetMode(DynamicIslandMode.Compact);
        }
    }

    public void ShowDistracted(string goal, string reason, TimeSpan elapsed)
    {
        UpdateContent(FocusStatus.Distracted, elapsed, goal, reason);
        ExpandedTitleText.Text = "You may be off track";
        SetMode(DynamicIslandMode.Alert);
        ScheduleMode(DynamicIslandMode.Compact, TimeSpan.FromSeconds(10));
    }

    public void ShowCompleted(TimeSpan elapsed, int distractionCount)
    {
        UpdateContent(FocusStatus.Focused, elapsed, GoalText.Text, $"Distractions: {distractionCount}");
        ExpandedTitleText.Text = "Session completed";
        SetMode(DynamicIslandMode.Completed);
        ScheduleMode(DynamicIslandMode.Hidden, TimeSpan.FromSeconds(3));
    }

    public void ShowStopped(TimeSpan elapsed, string message)
    {
        UpdateContent(FocusStatus.Unknown, elapsed, GoalText.Text, message);
        ExpandedTitleText.Text = "Session stopped";
        SetMode(DynamicIslandMode.Completed);
        ScheduleMode(DynamicIslandMode.Hidden, TimeSpan.FromSeconds(3));
    }

    public void SetMode(DynamicIslandMode mode)
    {
        _modeTimer.Stop();
        _mode = mode;

        if (mode == DynamicIslandMode.Hidden)
        {
            Hide();
            return;
        }

        CompactPanel.Visibility = mode == DynamicIslandMode.Compact
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExpandedPanel.Visibility = mode == DynamicIslandMode.Compact
            ? Visibility.Collapsed
            : Visibility.Visible;

        Width = mode == DynamicIslandMode.Compact ? CompactWidth : ExpandedWidth;
        Height = mode == DynamicIslandMode.Compact ? CompactHeight : ExpandedHeight;
        IslandRoot.CornerRadius = new CornerRadius(mode == DynamicIslandMode.Compact ? 38 : 30);

        if (!IsVisible)
        {
            Show();
        }

        PositionAtTopCenter();
    }

    private void UpdateContent(FocusStatus status, TimeSpan elapsed, string goal, string? reason)
    {
        _status = status;
        var statusText = FormatStatus(status);
        var elapsedText = ElapsedTimeFormatter.Format(elapsed);
        var brush = GetStatusBrush(status);

        StatusText.Text = statusText;
        StatusDot.Fill = brush;
        ExpandedStatusDot.Fill = brush;
        GoalText.Text = goal;
        ExpandedGoalText.Text = $"Goal: {goal}";
        ElapsedText.Text = elapsedText;
        ExpandedElapsedText.Text = elapsedText;
        ReasonText.Text = string.IsNullOrWhiteSpace(reason) ? $"Current status: {statusText}" : reason;

        if (_mode is DynamicIslandMode.Hidden or DynamicIslandMode.Compact)
        {
            ExpandedTitleText.Text = statusText;
        }
    }

    private void ScheduleMode(DynamicIslandMode mode, TimeSpan delay)
    {
        _modeTimer.Stop();
        _modeTimer.Tag = mode;
        _modeTimer.Interval = delay;
        _modeTimer.Start();
    }

    private void ModeTimer_Tick(object? sender, EventArgs e)
    {
        _modeTimer.Stop();
        if (_modeTimer.Tag is DynamicIslandMode mode)
        {
            SetMode(mode);
        }
    }

    private void PositionAtTopCenter()
    {
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - Width) / 2;
        Top = SystemParameters.WorkArea.Top + 20;
    }

    private void IslandRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindVisualParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        SetMode(_mode == DynamicIslandMode.Compact ? DynamicIslandMode.Expanded : DynamicIslandMode.Compact);
    }

    private void BackToFocusButton_Click(object sender, RoutedEventArgs e)
    {
        SetMode(DynamicIslandMode.Compact);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        SetMode(DynamicIslandMode.Compact);
    }

    private void DynamicIslandWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, extendedStyle | WsExToolWindow);
    }

    private static T? FindVisualParent<T>(DependencyObject? child) where T : DependencyObject
    {
        while (child is not null)
        {
            if (child is T match)
            {
                return match;
            }

            child = VisualTreeHelper.GetParent(child);
        }

        return null;
    }

    private static string FormatStatus(FocusStatus status)
    {
        return status switch
        {
            FocusStatus.Focused => "Focused",
            FocusStatus.Wandering => "Wandering",
            FocusStatus.Distracted => "Distracted",
            FocusStatus.Idle => "Idle",
            _ => "Monitoring"
        };
    }

    private static System.Windows.Media.Brush GetStatusBrush(FocusStatus status)
    {
        return status switch
        {
            FocusStatus.Focused => new SolidColorBrush(System.Windows.Media.Color.FromRgb(53, 211, 174)),
            FocusStatus.Wandering => new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 184, 66)),
            FocusStatus.Distracted => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 104, 87)),
            FocusStatus.Idle => new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 147, 163)),
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 147, 163))
        };
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newStyle);
}
