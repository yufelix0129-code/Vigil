using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using VigilWin.Core;
using VigilWin.Models;

namespace VigilWin.Services;

public sealed class AIService
{
    private readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(60)
    };

    public async Task<AIAnalysisResult> AnalyzeScreenshotAsync(
        string goal,
        byte[] screenshotJpeg,
        AIProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(goal);
        ArgumentNullException.ThrowIfNull(screenshotJpeg);

        if (!IsConfigured(config))
        {
            return new AIAnalysisResult
            {
                Status = FocusStatus.Unknown,
                Confidence = 0,
                Reason = "AI 配置不完整"
            };
        }

        try
        {
            var imageBase64 = Convert.ToBase64String(screenshotJpeg);
            var payload = new
            {
                model = config.Model,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new
                            {
                                type = "text",
                                text = PromptTemplates.BuildAnalysisPrompt(goal)
                            },
                            new
                            {
                                type = "image_url",
                                image_url = new
                                {
                                    url = $"data:image/jpeg;base64,{imageBase64}"
                                }
                            }
                        }
                    }
                },
                temperature = 0.1,
                max_tokens = 300
            };

            using var request = CreateRequest(config, payload);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AIAnalysisResult
                {
                    Status = FocusStatus.Unknown,
                    Confidence = 0,
                    Reason = $"AI 请求失败：{(int)response.StatusCode} {response.ReasonPhrase}"
                };
            }

            var content = ExtractMessageContent(responseBody);
            return ParseAnalysisResult(content);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new AIAnalysisResult
            {
                Status = FocusStatus.Unknown,
                Confidence = 0,
                Reason = $"AI 分析失败：{ex.Message}"
            };
        }
    }

    public async Task<string> GenerateSummaryAsync(
        FocusSession session,
        IReadOnlyList<FrameRecord> records,
        AIProviderConfig config,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured(config))
        {
            return SummaryService.BuildLocalSummary(session, records);
        }

        try
        {
            var payload = new
            {
                model = config.Model,
                messages = new object[]
                {
                    new
                    {
                        role = "user",
                        content = PromptTemplates.BuildSummaryPrompt(session, records)
                    }
                },
                temperature = 0.2,
                max_tokens = 800
            };

            using var request = CreateRequest(config, payload);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return SummaryService.BuildLocalSummary(session, records);
            }

            var content = ExtractMessageContent(responseBody);
            return string.IsNullOrWhiteSpace(content)
                ? SummaryService.BuildLocalSummary(session, records)
                : content.Trim();
        }
        catch
        {
            return SummaryService.BuildLocalSummary(session, records);
        }
    }

    private static bool IsConfigured(AIProviderConfig? config)
    {
        return config is not null
            && !string.IsNullOrWhiteSpace(config.BaseUrl)
            && !string.IsNullOrWhiteSpace(config.ApiKey)
            && !string.IsNullOrWhiteSpace(config.Model);
    }

    private static HttpRequestMessage CreateRequest(AIProviderConfig config, object payload)
    {
        var endpoint = $"{config.BaseUrl.TrimEnd('/')}/chat/completions";
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        return request;
    }

    private static string ExtractMessageContent(string responseBody)
    {
        using var document = JsonDocument.Parse(responseBody);
        var root = document.RootElement;
        var choices = root.GetProperty("choices");

        if (choices.GetArrayLength() == 0)
        {
            return string.Empty;
        }

        var content = choices[0].GetProperty("message").GetProperty("content");
        if (content.ValueKind == JsonValueKind.String)
        {
            return content.GetString() ?? string.Empty;
        }

        if (content.ValueKind == JsonValueKind.Array)
        {
            var builder = new StringBuilder();
            foreach (var part in content.EnumerateArray())
            {
                if (part.TryGetProperty("text", out var textElement))
                {
                    builder.Append(textElement.GetString());
                }
            }

            return builder.ToString();
        }

        return content.ToString();
    }

    private static AIAnalysisResult ParseAnalysisResult(string content)
    {
        var json = ExtractJsonObject(content);
        if (string.IsNullOrWhiteSpace(json))
        {
            return new AIAnalysisResult
            {
                Status = FocusStatus.Unknown,
                Confidence = 0,
                Reason = "AI 返回格式不是 JSON"
            };
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var statusText = root.TryGetProperty("status", out var statusElement)
                ? statusElement.GetString()
                : null;

            var status = ParseStatus(statusText);
            var confidence = ReadDouble(root, "confidence");
            var reason = root.TryGetProperty("reason", out var reasonElement)
                ? reasonElement.GetString() ?? string.Empty
                : string.Empty;

            return new AIAnalysisResult
            {
                Status = status,
                Confidence = Math.Clamp(confidence, 0, 1),
                Reason = string.IsNullOrWhiteSpace(reason) ? "AI 未提供原因" : reason
            };
        }
        catch (JsonException)
        {
            return new AIAnalysisResult
            {
                Status = FocusStatus.Unknown,
                Confidence = 0,
                Reason = "AI 返回 JSON 解析失败"
            };
        }
    }

    private static string? ExtractJsonObject(string content)
    {
        var trimmed = content.Trim();
        if (trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            trimmed = trimmed.Trim('`').Trim();
            if (trimmed.StartsWith("json", StringComparison.OrdinalIgnoreCase))
            {
                trimmed = trimmed[4..].Trim();
            }
        }

        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');
        if (start < 0 || end < start)
        {
            return null;
        }

        return trimmed[start..(end + 1)];
    }

    private static FocusStatus ParseStatus(string? statusText)
    {
        return statusText?.Trim().ToLowerInvariant() switch
        {
            "focused" => FocusStatus.Focused,
            "wandering" => FocusStatus.Wandering,
            "distracted" => FocusStatus.Distracted,
            "idle" => FocusStatus.Idle,
            _ => FocusStatus.Unknown
        };
    }

    private static double ReadDouble(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var element))
        {
            return 0;
        }

        return element.ValueKind switch
        {
            JsonValueKind.Number when element.TryGetDouble(out var number) => number,
            JsonValueKind.String when double.TryParse(element.GetString(), out var number) => number,
            _ => 0
        };
    }
}
