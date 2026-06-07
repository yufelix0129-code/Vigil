namespace VigilWin.Models;

public sealed class AIProviderConfig
{
    public string ProviderName { get; set; } = "OpenAI Compatible";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
