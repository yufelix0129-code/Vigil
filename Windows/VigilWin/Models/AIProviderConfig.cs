namespace VigilWin.Models;

public sealed class AIProviderConfig
{
    public string ProviderName { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;
}
