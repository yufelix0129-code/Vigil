using System.Windows;
using VigilWin.Models;
using VigilWin.Services;
using MessageBox = System.Windows.MessageBox;

namespace VigilWin.Views;

public partial class SettingsWindow : Window
{
    private readonly SettingsService _settingsService;
    private readonly LogService? _logService;
    private AppSettings _currentSettings = new();

    public SettingsWindow(SettingsService settingsService, LogService? logService = null)
    {
        InitializeComponent();
        _settingsService = settingsService;
        _logService = logService;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var settings = _settingsService.Load();
        _currentSettings = settings;
        ProviderNameTextBox.Text = settings.AIProvider.ProviderName;
        BaseUrlTextBox.Text = settings.AIProvider.BaseUrl;
        ApiKeyPasswordBox.Password = string.Empty;
        ApiKeyStatusText.Text = GetApiKeyStatusText(settings);
        ModelTextBox.Text = settings.AIProvider.Model;
        CaptureIntervalTextBox.Text = settings.CaptureIntervalSeconds.ToString();
        IdleThresholdTextBox.Text = settings.IdleThresholdSeconds.ToString();
        EnableOverlayCheckBox.IsChecked = settings.EnableOverlay;
        EnableDynamicIslandCheckBox.IsChecked = settings.EnableDynamicIsland;
        SaveScreenshotsCheckBox.IsChecked = settings.SaveScreenshots;
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings))
        {
            return;
        }

        _settingsService.Save(settings);
        _currentSettings = _settingsService.Load();
        _logService?.Info("Settings saved from SettingsWindow.");
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
            || string.IsNullOrWhiteSpace(settings.AIProvider.Model))
        {
            MessageBox.Show("请填写 Base URL 和 Model。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var hasUsableSavedApiKey = string.IsNullOrWhiteSpace(_currentSettings.AIProvider.ApiKeyError)
            && !string.IsNullOrWhiteSpace(settings.AIProvider.EncryptedApiKey);
        var hasApiKey = !string.IsNullOrWhiteSpace(ApiKeyPasswordBox.Password) || hasUsableSavedApiKey;
        if (!hasApiKey)
        {
            MessageBox.Show("请填写或保存 API Key。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        MessageBox.Show("配置格式正常。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private bool TryReadSettings(out AppSettings settings)
    {
        settings = new AppSettings();

        if (string.IsNullOrWhiteSpace(BaseUrlTextBox.Text))
        {
            MessageBox.Show("Base URL 不能为空。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (string.IsNullOrWhiteSpace(ModelTextBox.Text))
        {
            MessageBox.Show("Model 不能为空。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(CaptureIntervalTextBox.Text, out var captureIntervalSeconds) || captureIntervalSeconds < 1)
        {
            MessageBox.Show("Capture Interval Seconds 必须是大于 0 的整数。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        if (!int.TryParse(IdleThresholdTextBox.Text, out var idleThresholdSeconds) || idleThresholdSeconds < 1)
        {
            MessageBox.Show("Idle Threshold Seconds 必须是大于 0 的整数。", "Vigil", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }

        var newApiKey = ApiKeyPasswordBox.Password;
        var shouldPreserveExistingKey = string.IsNullOrWhiteSpace(newApiKey)
            && string.IsNullOrWhiteSpace(_currentSettings.AIProvider.ApiKeyError);
        settings.AIProvider = new AIProviderConfig
        {
            ProviderName = ProviderNameTextBox.Text.Trim(),
            BaseUrl = BaseUrlTextBox.Text.Trim(),
            ApiKey = newApiKey,
            Model = ModelTextBox.Text.Trim(),
            EncryptedApiKey = shouldPreserveExistingKey
                ? _currentSettings.AIProvider.EncryptedApiKey
                : string.Empty
        };
        settings.CaptureIntervalSeconds = captureIntervalSeconds;
        settings.IdleThresholdSeconds = idleThresholdSeconds;
        settings.EnableOverlay = EnableOverlayCheckBox.IsChecked == true;
        settings.EnableDynamicIsland = EnableDynamicIslandCheckBox.IsChecked == true;
        settings.SaveScreenshots = SaveScreenshotsCheckBox.IsChecked == true;

        return true;
    }

    private static string GetApiKeyStatusText(AppSettings settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.AIProvider.ApiKeyError))
        {
            return settings.AIProvider.ApiKeyError;
        }

        return string.IsNullOrWhiteSpace(settings.AIProvider.EncryptedApiKey)
            ? "未保存 API Key。"
            : "已保存 API Key，重新输入会覆盖。";
    }
}
