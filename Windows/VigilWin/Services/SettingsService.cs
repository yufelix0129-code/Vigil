using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using VigilWin.Models;

namespace VigilWin.Services;

public sealed class SettingsService
{
    private readonly SecureSecretService _secureSecretService;
    private readonly LogService? _logService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public SettingsService()
        : this(new SecureSecretService(), null)
    {
    }

    public SettingsService(SecureSecretService secureSecretService, LogService? logService = null)
    {
        _secureSecretService = secureSecretService;
        _logService = logService;
    }

    public static string AppDataDirectory
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("VIGILWIN_APPDATA");
            return string.IsNullOrWhiteSpace(overridePath)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "VigilWin")
                : overridePath;
        }
    }

    public static string SettingsPath => Path.Combine(AppDataDirectory, "settings.json");

    public static string ScreenshotDirectory => Path.Combine(AppDataDirectory, "Screenshots");

    public AppSettings Load()
    {
        Directory.CreateDirectory(AppDataDirectory);

        if (!File.Exists(SettingsPath))
        {
            var defaultSettings = CreateDefaultSettings();
            Save(defaultSettings);
            _logService?.Info("Settings file not found; created default settings.");
            return defaultSettings;
        }

        try
        {
            var json = File.ReadAllText(SettingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? CreateDefaultSettings();
            MigrateLegacyPlainTextApiKey(settings, json);
            Normalize(settings);
            DecryptApiKey(settings);
            _logService?.Info("Settings loaded successfully.");
            return settings;
        }
        catch (JsonException)
        {
            var backupPath = $"{SettingsPath}.broken-{DateTime.Now:yyyyMMddHHmmss}.bak";
            File.Copy(SettingsPath, backupPath, overwrite: true);

            var defaultSettings = CreateDefaultSettings();
            Save(defaultSettings);
            _logService?.Warn($"Settings JSON was invalid; backed up and recreated settings at {backupPath}.");
            return defaultSettings;
        }
        catch (Exception ex)
        {
            _logService?.Error("Settings load failed.", ex);
            throw;
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(AppDataDirectory);
        Normalize(settings);

        if (!string.IsNullOrWhiteSpace(settings.AIProvider.ApiKey))
        {
            settings.AIProvider.EncryptedApiKey = _secureSecretService.Protect(settings.AIProvider.ApiKey);
        }

        settings.AIProvider.ApiKeyError = null;

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsPath, json);
        _logService?.Info("Settings saved successfully.");
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
        settings.AIProvider.EncryptedApiKey ??= string.Empty;

        settings.CaptureIntervalSeconds = Math.Max(1, settings.CaptureIntervalSeconds);
        settings.IdleThresholdSeconds = Math.Max(1, settings.IdleThresholdSeconds);
    }

    private void DecryptApiKey(AppSettings settings)
    {
        if (!_secureSecretService.HasSecret(settings.AIProvider.EncryptedApiKey))
        {
            settings.AIProvider.ApiKey = string.Empty;
            settings.AIProvider.ApiKeyError = null;
            return;
        }

        try
        {
            settings.AIProvider.ApiKey = _secureSecretService.Unprotect(settings.AIProvider.EncryptedApiKey);
            settings.AIProvider.ApiKeyError = null;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            settings.AIProvider.ApiKey = string.Empty;
            settings.AIProvider.ApiKeyError = "API Key 解密失败，请重新填写";
            _logService?.Warn("API key decrypt failed; user must re-enter it.");
        }
    }

    private void MigrateLegacyPlainTextApiKey(AppSettings settings, string json)
    {
        if (!string.IsNullOrWhiteSpace(settings.AIProvider.EncryptedApiKey))
        {
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("AIProvider", out var provider))
            {
                return;
            }

            if (!provider.TryGetProperty("ApiKey", out var legacyApiKeyElement))
            {
                return;
            }

            var legacyApiKey = legacyApiKeyElement.GetString();
            if (string.IsNullOrWhiteSpace(legacyApiKey))
            {
                return;
            }

            settings.AIProvider.EncryptedApiKey = _secureSecretService.Protect(legacyApiKey);
            Save(settings);
            _logService?.Info("Migrated legacy plaintext API key to DPAPI-protected storage.");
        }
        catch (Exception ex)
        {
            _logService?.Warn($"Legacy API key migration skipped: {ex.Message}");
        }
    }
}
