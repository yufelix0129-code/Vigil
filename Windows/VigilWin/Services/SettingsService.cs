using System.IO;
using System.Text.Json;
using VigilWin.Models;

namespace VigilWin.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public static string AppDataDirectory =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VigilWin");

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    public static string ScreenshotDirectory => Path.Combine(AppDataDirectory, "Screenshots");

    public AppSettings Load()
    {
        Directory.CreateDirectory(AppDataDirectory);

        if (!File.Exists(SettingsPath))
        {
            var defaultSettings = CreateDefaultSettings();
            Save(defaultSettings);
            return defaultSettings;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefaultSettings();
            Normalize(settings);
            return settings;
        }
        catch (JsonException)
        {
            var backupPath = $"{SettingsPath}.broken-{DateTime.Now:yyyyMMddHHmmss}.bak";
            File.Copy(SettingsPath, backupPath, overwrite: true);

            var defaultSettings = CreateDefaultSettings();
            Save(defaultSettings);
            return defaultSettings;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDataDirectory);
        Normalize(settings);

        // TODO: Move ApiKey storage to Windows DPAPI or Credential Manager before shipping.
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
    }

    private static AppSettings CreateDefaultSettings()
    {
        return new AppSettings();
    }

    private static void Normalize(AppSettings settings)
    {
        settings.AIProvider ??= new AIProviderConfig();
        settings.AIProvider.ProviderName = string.IsNullOrWhiteSpace(settings.AIProvider.ProviderName)
            ? "OpenAI Compatible"
            : settings.AIProvider.ProviderName.Trim();
        settings.AIProvider.BaseUrl = string.IsNullOrWhiteSpace(settings.AIProvider.BaseUrl)
            ? "https://api.openai.com/v1"
            : settings.AIProvider.BaseUrl.Trim();
        settings.AIProvider.Model = string.IsNullOrWhiteSpace(settings.AIProvider.Model)
            ? "gpt-4o-mini"
            : settings.AIProvider.Model.Trim();
        settings.AIProvider.ApiKey ??= string.Empty;

        settings.CaptureIntervalSeconds = Math.Max(1, settings.CaptureIntervalSeconds);
        settings.IdleThresholdSeconds = Math.Max(5, settings.IdleThresholdSeconds);
    }
}
