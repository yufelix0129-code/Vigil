using System.Text.Json.Serialization;

namespace VigilWin.Models;

public sealed class AIProviderConfig
{
    public string ProviderName { get; set; } = "OpenAI Compatible";

    public string BaseUrl { get; set; } = "https://api.openai.com/v1";

    public string Model { get; set; } = string.Empty;

    public string EncryptedApiKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string? ApiKeyError { get; set; }
}
