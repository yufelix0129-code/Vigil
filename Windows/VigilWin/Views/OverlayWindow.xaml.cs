using System.Windows;
using System.Windows.Threading;
using VigilWin.Utilities;

namespace VigilWin.Views;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _closeTimer = new()
    {
        Interval = TimeSpan.FromSeconds(10)
    };
    private bool _isClosing;

    public OverlayWindow(string goal, string reason)
    {
        InitializeComponent();

        GoalText.Text = $"当前目标：{goal}";
        ReasonText.Text = $"AI 原因：{reason}";

        Loaded += OverlayWindow_Loaded;
        Closed += (_, _) => _closeTimer.Stop();
        _closeTimer.Tick += (_, _) => BeginClose();
    }

    private void OverlayWindow_Loaded(object sender, RoutedEventArgs e)
    {
        AnimationHelper.AnimateDouble(this, OpacityProperty, 0, 1, 220);
        AnimationHelper.FadeInScale(OverlayCard, fromScale: 0.96, durationMs: 260);
        _closeTimer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        BeginClose();
    }

    private void BeginClose()
    {
        if (_isClosing)
        {
            return;
        }

        _isClosing = true;
        _closeTimer.Stop();
        AnimationHelper.AnimateDouble(this, OpacityProperty, Opacity, 0, 220, easingMode: System.Windows.Media.Animation.EasingMode.EaseIn);
        AnimationHelper.RunAfterDelay(OverlayCard, 230, Close);
    }
}
