using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using VigilWin.Core;

namespace VigilWin.Views;

public partial class DynamicIslandWindow : Window
{
    private const double CompactWidth = 360;
    private const double CompactHeight = 78;
    private const double ExpandedWidth = 580;
    private const double ExpandedHeight = 224;
    private const double CompletedWidth = 540;
    private const double CompletedHeight = 188;
    private const int GwlExStyle = -20;
    private const int WsExToolWindow = 0x00000080;

    private DynamicIslandMode _mode = DynamicIslandMode.Hidden;
    private FocusStatus _status = FocusStatus.Unknown;
    private bool _isAnimating;
    private bool _isClosing;
    private string _goal = string.Empty;
    private TimeSpan _plannedDuration = TimeSpan.Zero;

    public DynamicIslandWindow()
    {
        InitializeComponent();
        SourceInitialized += DynamicIslandWindow_SourceInitialized;
    }

    public event EventHandler? HideRequested;

    public DynamicIslandMode CurrentMode => _mode;

    public bool IsClosing => _isClosing;

    public void ResetForNewSession()
    {
        _isClosing = false;
        _isAnimating = false;
        StopPropertyAnimations();
        Opacity = 1;
        IslandScale.ScaleX = 1;
        IslandScale.ScaleY = 1;
        IslandTranslate.Y = 0;
    }

    public void ShowSessionStarted(string goal, TimeSpan plannedDuration, IReadOnlyList<TimelineSegment> timeline)
    {
        _goal = goal;
        _plannedDuration = plannedDuration;
        ActionButtonsPanel.Visibility = Visibility.Visible;
        UpdateContent(FocusStatus.Unknown, TimeSpan.Zero, goal, plannedDuration, "Waiting for first analysis...", timeline);
        ExpandedTitleText.Text = goal;
        TransitionTo(DynamicIslandMode.Expanded, force: true);
    }

    public void UpdateStatus(
        FocusStatus status,
        TimeSpan elapsed,
        TimeSpan plannedDuration,
        string goal,
        string? reason,
        IReadOnlyList<TimelineSegment> timeline)
    {
        _goal = goal;
        _plannedDuration = plannedDuration;
        UpdateContent(status, elapsed, goal, plannedDuration, reason, timeline);

        if (_mode is DynamicIslandMode.Hidden && !_isClosing)
        {
            ActionButtonsPanel.Visibility = Visibility.Visible;
            TransitionTo(DynamicIslandMode.Compact, force: true);
        }
    }

    public void ShowDistracted(
        string goal,
        string reason,
        TimeSpan elapsed,
        TimeSpan plannedDuration,
        IReadOnlyList<TimelineSegment> timeline)
    {
        _goal = goal;
        _plannedDuration = plannedDuration;
        ActionButtonsPanel.Visibility = Visibility.Visible;
        UpdateContent(FocusStatus.Distracted, elapsed, goal, plannedDuration, reason, timeline);
        ExpandedTitleText.Text = "You may be off track";
        TransitionTo(DynamicIslandMode.Alert, force: true);
    }

    public void ShowCompleted(
        string goal,
        TimeSpan elapsed,
        TimeSpan plannedDuration,
        int distractionCount,
        IReadOnlyList<TimelineSegment> timeline)
    {
        _goal = goal;
        _plannedDuration = plannedDuration;
        ActionButtonsPanel.Visibility = Visibility.Collapsed;
        UpdateContent(
            FocusStatus.Focused,
            elapsed,
            goal,
            plannedDuration,
            $"Total time: {ElapsedTimeFormatter.Format(elapsed)} · Distractions: {distractionCount}",
            timeline,
            forceCompleteProgress: true);
        ExpandedTitleText.Text = "Session completed";
        TransitionTo(DynamicIslandMode.Completed, force: true);
    }

    public void ShowStopped(
        string goal,
        TimeSpan elapsed,
        TimeSpan plannedDuration,
        string message,
        IReadOnlyList<TimelineSegment> timeline)
    {
        _goal = goal;
        _plannedDuration = plannedDuration;
        ActionButtonsPanel.Visibility = Visibility.Collapsed;
        UpdateContent(FocusStatus.Unknown, elapsed, goal, plannedDuration, message, timeline);
        ExpandedTitleText.Text = "Session stopped";
        TransitionTo(DynamicIslandMode.Completed, force: true);
    }

    public void TransitionTo(DynamicIslandMode mode, bool force = false)
    {
        if (_isClosing && mode is not DynamicIslandMode.Hidden)
        {
            _isClosing = false;
        }

        if (_isAnimating && !force)
        {
            return;
        }

        if (mode == DynamicIslandMode.Hidden)
        {
            FadeOutAndClose();
            return;
        }

        var target = GetLayout(mode);
        var wasVisible = IsVisible;
        var previousMode = _mode;
        var oldContent = GetContentForMode(_mode);
        var newContent = GetContentForMode(mode);

        StopPropertyAnimations();
        _mode = mode;
        IslandRoot.CornerRadius = new CornerRadius(target.CornerRadius);
        if (mode == DynamicIslandMode.Compact && previousMode is not DynamicIslandMode.Completed)
        {
            ExpandedTitleText.Text = _goal;
        }

        if (!wasVisible)
        {
            Width = target.Width;
            Height = target.Height;
            SetContentVisibility(mode);
            Opacity = 0;
            IslandScale.ScaleX = 0.98;
            IslandScale.ScaleY = 0.98;
            IslandTranslate.Y = -6;
            PositionAtTopCenter(target.Width, mode);
            Show();
            AnimateShowIn(target);
            return;
        }

        _isAnimating = true;
        oldContent.Visibility = Visibility.Visible;
        newContent.Visibility = Visibility.Visible;
        oldContent.Opacity = oldContent == newContent ? 1 : oldContent.Opacity;
        newContent.Opacity = oldContent == newContent ? 1 : 0;

        var duration = mode switch
        {
            DynamicIslandMode.Alert => TimeSpan.FromMilliseconds(300),
            DynamicIslandMode.Compact => TimeSpan.FromMilliseconds(220),
            DynamicIslandMode.Completed => TimeSpan.FromMilliseconds(260),
            _ => TimeSpan.FromMilliseconds(260)
        };

        if (oldContent != newContent)
        {
            AnimateOpacity(oldContent, 0, TimeSpan.FromMilliseconds(80), TimeSpan.Zero);
            AnimateOpacity(newContent, 1, TimeSpan.FromMilliseconds(150), TimeSpan.FromMilliseconds(90));
        }

        var centerX = Left + Width / 2;
        var targetLeft = centerX - target.Width / 2;
        var targetTop = GetTargetTop(previousMode, mode);
        var sizeEase = CreateTransitionEase(mode);
        AnimateDouble(this, WidthProperty, Width, target.Width, duration, TimeSpan.Zero, sizeEase);
        AnimateDouble(this, LeftProperty, Left, targetLeft, duration, TimeSpan.Zero, sizeEase);
        AnimateDouble(this, TopProperty, Top, targetTop, duration, TimeSpan.Zero, sizeEase);
        AnimateDouble(IslandShadow, System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, IslandShadow.BlurRadius, target.ShadowBlur, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandShadow, System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, IslandShadow.Opacity, target.ShadowOpacity, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandScale, ScaleTransform.ScaleXProperty, IslandScale.ScaleX, target.Scale, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandScale, ScaleTransform.ScaleYProperty, IslandScale.ScaleY, target.Scale, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseOut));

        var completion = new DoubleAnimation(Height, target.Height, duration)
        {
            BeginTime = TimeSpan.Zero,
            EasingFunction = sizeEase
        };
        completion.Completed += (_, _) =>
        {
            Width = target.Width;
            Height = target.Height;
            Left = targetLeft;
            Top = targetTop;
            SetContentVisibility(mode);
            IslandScale.ScaleX = target.Scale;
            IslandScale.ScaleY = target.Scale;
            IslandShadow.BlurRadius = target.ShadowBlur;
            IslandShadow.Opacity = target.ShadowOpacity;
            _isAnimating = false;
        };
        BeginAnimation(HeightProperty, completion);
    }

    public void FadeOutAndClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _mode = DynamicIslandMode.Hidden;
        StopPropertyAnimations();

        if (!IsVisible)
        {
            Close();
            return;
        }

        _isAnimating = true;
        var duration = TimeSpan.FromMilliseconds(220);
        AnimateDouble(IslandScale, ScaleTransform.ScaleXProperty, IslandScale.ScaleX, 0.985, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseIn));
        AnimateDouble(IslandScale, ScaleTransform.ScaleYProperty, IslandScale.ScaleY, 0.985, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseIn));
        AnimateDouble(IslandTranslate, TranslateTransform.YProperty, IslandTranslate.Y, -8, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseIn));
        AnimateDouble(IslandShadow, System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, IslandShadow.Opacity, 0.08, duration, TimeSpan.Zero, CreateEase(EasingMode.EaseIn));

        var fade = new DoubleAnimation(Opacity, 0, duration)
        {
            EasingFunction = CreateEase(EasingMode.EaseIn)
        };
        fade.Completed += (_, _) =>
        {
            _isAnimating = false;
            Close();
        };
        BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateShowIn(IslandLayout target)
    {
        _isAnimating = true;
        AnimateDouble(this, OpacityProperty, 0, 1, TimeSpan.FromMilliseconds(180), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandScale, ScaleTransform.ScaleXProperty, 0.985, target.Scale, TimeSpan.FromMilliseconds(220), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandScale, ScaleTransform.ScaleYProperty, 0.985, target.Scale, TimeSpan.FromMilliseconds(220), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandTranslate, TranslateTransform.YProperty, -6, 0, TimeSpan.FromMilliseconds(220), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandShadow, System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, IslandShadow.BlurRadius, target.ShadowBlur, TimeSpan.FromMilliseconds(220), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
        AnimateDouble(IslandShadow, System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, IslandShadow.Opacity, target.ShadowOpacity, TimeSpan.FromMilliseconds(220), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));

        var completion = new DoubleAnimation(IslandTranslate.Y, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = CreateEase(EasingMode.EaseOut)
        };
        completion.Completed += (_, _) =>
        {
            Opacity = 1;
            IslandScale.ScaleX = target.Scale;
            IslandScale.ScaleY = target.Scale;
            IslandTranslate.Y = 0;
            IslandShadow.BlurRadius = target.ShadowBlur;
            IslandShadow.Opacity = target.ShadowOpacity;
            _isAnimating = false;
        };
        IslandTranslate.BeginAnimation(TranslateTransform.YProperty, completion);
    }

    private void UpdateContent(
        FocusStatus status,
        TimeSpan elapsed,
        string goal,
        TimeSpan plannedDuration,
        string? reason,
        IReadOnlyList<TimelineSegment> timeline,
        bool forceCompleteProgress = false)
    {
        _status = status;
        var statusText = FormatStatus(status);
        var elapsedText = ElapsedTimeFormatter.Format(elapsed);
        var brush = GetStatusBrush(status);

        CompactStatusDot.Fill = brush;
        ExpandedStatusDot.Fill = brush;
        CompactGoalText.Text = goal;
        ExpandedGoalText.Text = $"Goal: {goal}";
        CompactElapsedText.Text = elapsedText;
        ExpandedElapsedText.Text = elapsedText;
        ReasonText.Text = string.IsNullOrWhiteSpace(reason) ? GetDefaultReason(statusText, status) : reason;
        IslandRoot.BorderBrush = status == FocusStatus.Distracted
            ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 104, 87)) { Opacity = 0.48 }
            : (System.Windows.Media.Brush)FindResource("IslandBorderBrush");

        if (_mode is DynamicIslandMode.Hidden or DynamicIslandMode.Compact or DynamicIslandMode.Expanded)
        {
            ExpandedTitleText.Text = goal;
        }

        UpdateProgress(elapsed, plannedDuration, timeline, status, forceCompleteProgress);
    }

    private void UpdateProgress(
        TimeSpan elapsed,
        TimeSpan plannedDuration,
        IReadOnlyList<TimelineSegment> timeline,
        FocusStatus currentStatus,
        bool forceCompleteProgress)
    {
        double percent;
        if (forceCompleteProgress)
        {
            percent = 100;
        }
        else if (plannedDuration.TotalSeconds <= 0)
        {
            percent = 18;
        }
        else
        {
            percent = Math.Clamp(elapsed.TotalSeconds / plannedDuration.TotalSeconds * 100, 0, 100);
        }

        CompactTimelineBar.UpdateTimeline(timeline, elapsed, plannedDuration, currentStatus, forceCompleteProgress);
        ExpandedTimelineBar.UpdateTimeline(timeline, elapsed, plannedDuration, currentStatus, forceCompleteProgress);

        ProgressPercentText.Text = $"{percent:0}%";
        ProgressLabelText.Text = plannedDuration.TotalSeconds > 0
            ? $"Progress · {ElapsedTimeFormatter.Format(elapsed)} / {ElapsedTimeFormatter.Format(plannedDuration)}"
            : "Focus timeline warming up";
    }

    private void PositionAtTopCenter(double width, DynamicIslandMode mode)
    {
        Left = GetCenteredLeft(width);
        Top = GetRestingTop(mode);
    }

    private double GetCenteredLeft(double width)
    {
        return SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - width) / 2;
    }

    private void SetContentVisibility(DynamicIslandMode mode)
    {
        var compact = mode == DynamicIslandMode.Compact;
        CompactContent.Visibility = compact ? Visibility.Visible : Visibility.Collapsed;
        ExpandedContent.Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
        CompactContent.Opacity = compact ? 1 : 0;
        ExpandedContent.Opacity = compact ? 0 : 1;
    }

    private FrameworkElement GetContentForMode(DynamicIslandMode mode)
    {
        return mode == DynamicIslandMode.Compact ? CompactContent : ExpandedContent;
    }

    private void StopPropertyAnimations()
    {
        BeginAnimation(WidthProperty, null);
        BeginAnimation(HeightProperty, null);
        BeginAnimation(LeftProperty, null);
        BeginAnimation(TopProperty, null);
        BeginAnimation(OpacityProperty, null);
        CompactContent.BeginAnimation(OpacityProperty, null);
        ExpandedContent.BeginAnimation(OpacityProperty, null);
        IslandScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        IslandScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        IslandTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.BlurRadiusProperty, null);
        IslandShadow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, null);
    }

    private void IslandRoot_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isAnimating || _isClosing || _mode is DynamicIslandMode.Completed)
        {
            return;
        }

        if (FindVisualParent<System.Windows.Controls.Button>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        TransitionTo(_mode == DynamicIslandMode.Compact ? DynamicIslandMode.Expanded : DynamicIslandMode.Compact);
    }

    private void BackToFocusButton_Click(object sender, RoutedEventArgs e)
    {
        TransitionTo(DynamicIslandMode.Compact);
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        HideRequested?.Invoke(this, EventArgs.Empty);
    }

    private void IslandRoot_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        AnimateDouble(IslandShadow, System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, IslandShadow.Opacity, 0.40, TimeSpan.FromMilliseconds(160), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
    }

    private void IslandRoot_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isClosing)
        {
            return;
        }

        var target = GetLayout(_mode);
        AnimateDouble(IslandShadow, System.Windows.Media.Effects.DropShadowEffect.OpacityProperty, IslandShadow.Opacity, target.ShadowOpacity, TimeSpan.FromMilliseconds(180), TimeSpan.Zero, CreateEase(EasingMode.EaseOut));
    }

    private void DynamicIslandWindow_SourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        var extendedStyle = GetWindowLong(handle, GwlExStyle);
        SetWindowLong(handle, GwlExStyle, extendedStyle | WsExToolWindow);
    }

    private static void AnimateOpacity(UIElement element, double to, TimeSpan duration, TimeSpan beginTime)
    {
        AnimateDouble(element, OpacityProperty, element.Opacity, to, duration, beginTime, CreateEase(EasingMode.EaseOut));
    }

    private static void AnimateDouble(
        DependencyObject target,
        DependencyProperty property,
        double from,
        double to,
        TimeSpan duration,
        TimeSpan beginTime,
        IEasingFunction easing)
    {
        var animation = new DoubleAnimation(from, to, duration)
        {
            BeginTime = beginTime,
            EasingFunction = easing
        };
        switch (target)
        {
            case UIElement element:
                element.BeginAnimation(property, animation);
                break;
            case Animatable animatable:
                animatable.BeginAnimation(property, animation);
                break;
            default:
                throw new InvalidOperationException($"Animation target {target.GetType().Name} is not animatable.");
        }
    }

    private static IEasingFunction CreateEase(EasingMode mode)
    {
        return new CubicEase { EasingMode = mode };
    }

    private static IEasingFunction CreateTransitionEase(DynamicIslandMode mode)
    {
        return mode == DynamicIslandMode.Compact
            ? new CubicEase { EasingMode = EasingMode.EaseInOut }
            : new QuinticEase { EasingMode = EasingMode.EaseOut };
    }

    private static IslandLayout GetLayout(DynamicIslandMode mode)
    {
        return mode switch
        {
            DynamicIslandMode.Compact => new IslandLayout(CompactWidth, CompactHeight, 38, 32, 0.30, 1),
            DynamicIslandMode.Completed => new IslandLayout(CompletedWidth, CompletedHeight, 32, 30, 0.28, 1),
            DynamicIslandMode.Alert => new IslandLayout(ExpandedWidth, ExpandedHeight, 32, 38, 0.38, 1),
            _ => new IslandLayout(ExpandedWidth, ExpandedHeight, 32, 34, 0.34, 1)
        };
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
            _ => "No analysis yet"
        };
    }

    private static string GetDefaultReason(string statusText, FocusStatus status)
    {
        return status == FocusStatus.Unknown
            ? "Waiting for first analysis..."
            : $"Current status: {statusText}";
    }

    private double GetTargetTop(DynamicIslandMode previousMode, DynamicIslandMode targetMode)
    {
        if (previousMode == targetMode)
        {
            return Top;
        }

        return GetRestingTop(targetMode);
    }

    private static double GetRestingTop(DynamicIslandMode mode)
    {
        return SystemParameters.WorkArea.Top + (mode == DynamicIslandMode.Compact ? 20 : 17);
    }

    private static System.Windows.Media.Brush GetStatusBrush(FocusStatus status)
    {
        return status switch
        {
            FocusStatus.Focused => new SolidColorBrush(System.Windows.Media.Color.FromRgb(53, 211, 174)),
            FocusStatus.Wandering => new SolidColorBrush(System.Windows.Media.Color.FromRgb(245, 184, 66)),
            FocusStatus.Distracted => new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 104, 87)),
            FocusStatus.Idle => new SolidColorBrush(System.Windows.Media.Color.FromRgb(193, 174, 104)),
            _ => new SolidColorBrush(System.Windows.Media.Color.FromRgb(139, 147, 163))
        };
    }

    [DllImport("user32.dll")]
    private static extern int GetWindowLong(IntPtr windowHandle, int index);

    [DllImport("user32.dll")]
    private static extern int SetWindowLong(IntPtr windowHandle, int index, int newStyle);

    private readonly record struct IslandLayout(
        double Width,
        double Height,
        double CornerRadius,
        double ShadowBlur,
        double ShadowOpacity,
        double Scale);
}
