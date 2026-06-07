using System.Windows;
using System.Windows.Threading;

namespace VigilWin.Views;

public partial class OverlayWindow : Window
{
    private readonly DispatcherTimer _closeTimer = new()
    {
        Interval = TimeSpan.FromSeconds(10)
    };

    public OverlayWindow(string goal, string reason)
    {
        InitializeComponent();

        GoalText.Text = $"当前目标：{goal}";
        ReasonText.Text = $"AI 原因：{reason}";

        Loaded += (_, _) => _closeTimer.Start();
        Closed += (_, _) => _closeTimer.Stop();
        _closeTimer.Tick += (_, _) => Close();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
