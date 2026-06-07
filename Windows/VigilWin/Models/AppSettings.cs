namespace VigilWin.Models;

public sealed class AppSettings
{
    public AIProviderConfig AIProvider { get; set; } = new()
    {
        ProviderName = "OpenAI Compatible",
        BaseUrl = "https://api.openai.com/v1",
        ApiKey = string.Empty,
        Model = "gpt-4o-mini"
    };

    public int CaptureIntervalSeconds { get; set; } = 5;

    public int IdleThresholdSeconds { get; set; } = 60;

    public bool EnableOverlay { get; set; } = true;

    public bool SaveScreenshots { get; set; } = false;
}
