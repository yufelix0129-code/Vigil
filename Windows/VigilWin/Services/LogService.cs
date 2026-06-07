using System.IO;

namespace VigilWin.Services;

public sealed class LogService
{
    private readonly object _syncRoot = new();

    public static string LogDirectory => Path.Combine(SettingsService.AppDataDirectory, "logs");

    public static string LogPath => Path.Combine(LogDirectory, "vigil.log");

    public void Info(string message)
    {
        Write("INFO", message);
    }

    public void Warn(string message)
    {
        Write("WARN", message);
    }

    public void Error(string message, Exception? ex = null)
    {
        var text = ex is null
            ? message
            : $"{message} {ex.GetType().Name}: {ex.Message}";
        Write("ERROR", text);
    }

    private void Write(string level, string message)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{level}] {message}{Environment.NewLine}";

            lock (_syncRoot)
            {
                File.AppendAllText(LogPath, line);
            }
        }
        catch
        {
            // Logging must never crash the app.
        }
    }
}
