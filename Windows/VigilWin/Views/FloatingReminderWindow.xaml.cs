using System.Windows;
using System.Windows.Threading;

namespace VigilWin.Views;

public partial class FloatingReminderWindow : Window
{
    private readonly DispatcherTimer _closeTimer = new()
    {
        Interval = TimeSpan.FromSeconds(10)
    };

    public FloatingReminderWindow(string goal, string reason)
    {
        InitializeComponent();

        GoalText.Text = $"当前目标：{goal}";
        ReasonText.Text = $"AI 原因：{reason}";

        Loaded += FloatingReminderWindow_Loaded;
        Closed += (_, _) => _closeTimer.Stop();
        _closeTimer.Tick += (_, _) => Close();
    }

    private void FloatingReminderWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Left = SystemParameters.WorkArea.Left + (SystemParameters.WorkArea.Width - ActualWidth) / 2;
        Top = SystemParameters.WorkArea.Top + 24;
        _closeTimer.Start();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
