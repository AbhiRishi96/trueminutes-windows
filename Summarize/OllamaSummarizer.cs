using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;

namespace TrueMinutes.Windows.Summarize;

/// LLM summarization via Ollama's local HTTP API — direct port of macOS OllamaSummarizer.swift.
/// The HTTP contract is identical; no platform changes needed.
public sealed class OllamaSummarizer
{
    private readonly HttpClient _http;
    private readonly string _model;
    private readonly Uri _chatEndpoint;

    public static string DefaultBaseUrl =>
        Environment.GetEnvironmentVariable("OLLAMA_HOST") ?? "http://127.0.0.1:11434";

    public OllamaSummarizer(string? baseUrl = null, string? model = null)
    {
        var url = baseUrl ?? DefaultBaseUrl;
        _model = model ?? "qwen2.5:7b";
        _http = new HttpClient { BaseAddress = new Uri(url), Timeout = TimeSpan.FromSeconds(180) };
        _chatEndpoint = new Uri($"{url.TrimEnd('/')}/api/chat");
    }

    /// Summarize a transcript into structured JSON (decisions, action items, etc.)
    public async Task<MeetingSummary> SummarizeAsync(string transcript, string systemPrompt, CancellationToken ct = default)
    {
        var body = new OllamaChatRequest
        {
            Model = _model,
            Stream = false,
            Format = "json",
            Messages =
            [
                new OllamaMessage { Role = "system", Content = systemPrompt },
                new OllamaMessage { Role = "user",   Content = transcript  }
            ]
        };
        var json = JsonSerializer.Serialize(body);
        var request = new HttpRequestMessage(HttpMethod.Post, _chatEndpoint)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        using var resp = await _http.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();

        var respJson = await resp.Content.ReadAsStringAsync(ct);
        var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(respJson)!;
        return JsonSummaryDecoder.Decode(envelope.Message.Content);
    }

    /// Free-text paragraph reflow — no JSON format, plain prose output.
    public async Task<string> ChatAsync(string systemPrompt, string userContent, CancellationToken ct = default)
    {
        var body = new OllamaChatRequest
        {
            Model = _model,
            Stream = false,
            Format = "json",
            Options = new OllamaOptions { Temperature = 0.2f },
            Messages =
            [
                new OllamaMessage { Role = "system", Content = systemPrompt },
                new OllamaMessage { Role = "user",   Content = userContent  }
            ]
        };
        var request = new HttpRequestMessage(HttpMethod.Post, _chatEndpoint)
        {
            Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json")
        };
        using var resp = await _http.SendAsync(request, ct);
        resp.EnsureSuccessStatusCode();
        var respJson = await resp.Content.ReadAsStringAsync(ct);
        var envelope = JsonSerializer.Deserialize<OllamaChatResponse>(respJson)!;
        return envelope.Message.Content;
    }

    // --- DTOs (mirrors macOS OllamaChatResponse) ---

    private sealed class OllamaChatRequest
    {
        [JsonPropertyName("model")]   public string Model { get; set; } = "";
        [JsonPropertyName("stream")]  public bool Stream { get; set; }
        [JsonPropertyName("format")]  public string Format { get; set; } = "json";
        [JsonPropertyName("options")] public OllamaOptions? Options { get; set; }
        [JsonPropertyName("messages")] public OllamaMessage[] Messages { get; set; } = [];
    }

    private sealed class OllamaOptions
    {
        [JsonPropertyName("temperature")] public float Temperature { get; set; }
    }

    private sealed class OllamaMessage
    {
        [JsonPropertyName("role")]    public string Role { get; set; } = "";
        [JsonPropertyName("content")] public string Content { get; set; } = "";
    }

    private sealed class OllamaChatResponse
    {
        [JsonPropertyName("message")] public OllamaMessage Message { get; set; } = new();
    }
}

// --- Placeholder types (flesh out with actual JSON parsing) ---

public sealed class MeetingSummary
{
    public string Summary { get; set; } = "";
    public List<string> Decisions { get; set; } = [];
    public List<string> ActionItems { get; set; } = [];
    public List<string> OpenQuestions { get; set; } = [];
}

public static class JsonSummaryDecoder
{
    public static MeetingSummary Decode(string json)
    {
        try
        {
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            return new MeetingSummary
            {
                Summary = root.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "",
                Decisions = ParseStringArray(root, "decisions"),
                ActionItems = ParseStringArray(root, "action_items"),
                OpenQuestions = ParseStringArray(root, "open_questions")
            };
        }
        catch
        {
            return new MeetingSummary { Summary = json }; // extractive fallback
        }
    }

    private static List<string> ParseStringArray(JsonElement root, string key)
    {
        if (!root.TryGetProperty(key, out var arr)) return [];
        var result = new List<string>();
        foreach (var el in arr.EnumerateArray())
        {
            var s = el.ValueKind == JsonValueKind.String ? el.GetString() : el.ToString();
            if (!string.IsNullOrWhiteSpace(s)) result.Add(s!);
        }
        return result;
    }
}
