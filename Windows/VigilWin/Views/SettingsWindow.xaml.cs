using System.Windows;
using VigilWin.Models;
using VigilWin.Services;
using MessageBox = System.Windows.MessageBox;

namespace VigilWin.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;

    public SettingsWindow(SettingsService settingsService)
    {
        InitializeComponent();
        _settingsService = settingsService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        ProviderNameTextBox.Text = settings.AIProvider.ProviderName;
        BaseUrlTextBox.Text = settings.AIProvider.BaseUrl;
        ApiKeyPasswordBox.Password = settings.AIProvider.ApiKey;
        ModelTextBox.Text = settings.AIProvider.Model;
        CaptureIntervalTextBox.Text = settings.CaptureIntervalSeconds.ToString();
        IdleThresholdTextBox.Text = settings.IdleThresholdSeconds.ToString();
        EnableOverlayCheckBox.IsChecked = settings.EnableOverlay;
        SaveScreenshotsCheckBox.IsChecked = settings.SaveScreenshots;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
        {
            return;
        }

        _settingsService.Save(settings);
        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void TestButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.AIProvider.BaseUrl)
            || string.IsNullOrWhiteSpace(settings.AIProvider.ApiKey)
            || string.IsNullOrWhiteSpace(settings.AIProvider.Model))
        {
            MessageBox.Show("请填写 Base URL、API Key 和 Model。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("AI 配置字段已填写。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool TryReadSettings(out AppSettings settings)
    {
        settings = new AppSettings();

        if (!int.TryParse(CaptureIntervalTextBox.Text, out var captureIntervalSeconds) || captureIntervalSeconds < 1)
        {
            MessageBox.Show("Capture Interval Seconds 必须是大于 0 的整数。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(IdleThresholdTextBox.Text, out var idleThresholdSeconds) || idleThresholdSeconds < 5)
        {
            MessageBox.Show("Idle Threshold Seconds 必须是至少 5 的整数。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        settings.AIProvider = new AIProviderConfig
        {
            ProviderName = ProviderNameTextBox.Text.Trim(),
            BaseUrl = BaseUrlTextBox.Text.Trim(),
            ApiKey = ApiKeyPasswordBox.Password,
            Model = ModelTextBox.Text.Trim()
        };
        settings.CaptureIntervalSeconds = captureIntervalSeconds;
        settings.IdleThresholdSeconds = idleThresholdSeconds;
        settings.EnableOverlay = EnableOverlayCheckBox.IsChecked == true;
        settings.SaveScreenshots = SaveScreenshotsCheckBox.IsChecked == true;

        return true;
    }
}
