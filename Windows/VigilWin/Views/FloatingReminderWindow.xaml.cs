using System.Windows;
using System.Windows.Threading;
using VigilWin.Utilities;

namespace VigilWin.Views;

public partial class FloatingReminderWindow : Window
{
    private readonly DispatcherTimer _closeTimer = new()
    {
        Interval = TimeSpan.FromSeconds(10)
    };
    private bool _isClosing;

    public FloatingReminderWindow(string goal, string reason)
    {
        InitializeComponent();

        GoalText.Text = $"当前目标：{goal}";
        ReasonText.Text = $"AI 原因：{reason}";

        Loaded += FloatingReminderWindow_Loaded;
        Closed += (_, _) => _closeTimer.Stop();
        _closeTimer.Tick += (_, _) => BeginClose();
    }

    private void FloatingReminderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - ActualWidth) / 2;
        Top = SystemParameters.WorkArea.Top + 24;
        AnimationHelper.FadeInUp(ReminderShell, fromY: -10, durationMs: 240);
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
        AnimationHelper.FadeOutUp(ReminderShell, () => Close(), toY: -10, durationMs: 200);
    }
}
