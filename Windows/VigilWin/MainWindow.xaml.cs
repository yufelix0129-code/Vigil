using System.Windows;
using System.Windows.Controls;

namespace VigilWin;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
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

    private void StartFocusButton_Click(object sender, RoutedEventArgs e)
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

        CurrentStatusText.Text = "当前状态：运行中";
    }

    private void StopFocusButton_Click(object sender, RoutedEventArgs e)
    {
        CurrentStatusText.Text = "当前状态：已停止";
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        var settingsWindow = new Views.SettingsWindow
        {
            Owner = this
        };

        settingsWindow.ShowDialog();
    }
}
