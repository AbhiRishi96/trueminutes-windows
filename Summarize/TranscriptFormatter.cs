using System.Text.Json;
using System.Text;

namespace TrueMinutes.Windows.Summarize;

/// Turns raw fragmented Whisper segments into clean speaker-attributed paragraphs.
/// Direct C# port of macOS TranscriptFormatter.swift.
public static class TranscriptFormatter
{
    public sealed record RawTurn(string SpeakerKey, string Text, int StartMs);

    /// Instant heuristic grouping (no LLM). Merges consecutive same-speaker turns.
    public static List<SpeakerParagraph> HeuristicParagraphs(
        IEnumerable<RawTurn> turns, int gapMs = 9_000)
    {
        var ordered = turns.OrderBy(t => t.StartMs).ToList();
        var result  = new List<SpeakerParagraph>();
        var buffer  = new List<string>();
        string? bufferSpeaker = null;
        int bufferStart = 0, lastStart = 0;

        void Flush()
        {
            if (bufferSpeaker is null || buffer.Count == 0) return;
            var text = string.Join(" ", buffer).Trim();
            if (!string.IsNullOrEmpty(text))
                result.Add(new SpeakerParagraph(bufferSpeaker, text, bufferStart));
            buffer.Clear();
            bufferSpeaker = null;
        }

        foreach (var turn in ordered)
        {
            var piece = turn.Text.Trim();
            if (string.IsNullOrEmpty(piece)) continue;
            bool speakerChanged = bufferSpeaker is not null && bufferSpeaker != turn.SpeakerKey;
            bool longPause      = bufferSpeaker is not null && (turn.StartMs - lastStart) > gapMs;
            if (speakerChanged || longPause) Flush();
            bufferSpeaker ??= turn.SpeakerKey;
            if (bufferStart == 0 || speakerChanged || longPause) bufferStart = turn.StartMs;
            buffer.Add(piece);
            lastStart = turn.StartMs;
        }
        Flush();
        return result;
    }

    /// LLM reflow via Ollama. Throws if model unreachable — callers fall back to heuristic.
    public static async Task<List<SpeakerParagraph>> FormatParagraphsAsync(
        string labeledText,
        string? model = null,
        CancellationToken ct = default)
    {
        var trimmed = labeledText.Trim();
        if (string.IsNullOrEmpty(trimmed)) return [];

        var baseUrl  = OllamaSummarizer.DefaultBaseUrl;
        var modelName = model ?? "qwen2.5:7b";
        var endpoint  = $"{baseUrl.TrimEnd('/')}/api/chat";

        var body = JsonSerializer.Serialize(new
        {
            model    = modelName,
            stream   = false,
            format   = "json",
            options  = new { temperature = 0.2 },
            messages = new[]
            {
                new { role = "system", content = SystemPrompt },
                new { role = "user",   content = trimmed }
            }
        });

        using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        using var req  = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, endpoint)
        {
            Content = new System.Net.Http.StringContent(body, Encoding.UTF8, "application/json")
        };
        using var resp = await http.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        var json    = await resp.Content.ReadAsStringAsync(ct);
        var envelope = JsonDocument.Parse(json).RootElement;
        var content = envelope.GetProperty("message").GetProperty("content").GetString() ?? "";

        return ParseParagraphs(content);
    }

    private static List<SpeakerParagraph> ParseParagraphs(string content)
    {
        var jsonStr = ExtractJsonObject(content) ?? content;
        try
        {
            var doc   = JsonDocument.Parse(jsonStr);
            var arr   = doc.RootElement.GetProperty("paragraphs");
            var paras = new List<SpeakerParagraph>();
            foreach (var el in arr.EnumerateArray())
            {
                var speaker = el.GetProperty("speaker").GetString() ?? "";
                var text    = el.GetProperty("text").GetString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(text)) paras.Add(new SpeakerParagraph(speaker, text));
            }
            return paras.Count > 0 ? paras : throw new InvalidDataException("empty");
        }
        catch { throw new InvalidDataException($"Could not parse paragraphs from: {content[..Math.Min(200, content.Length)]}"); }
    }

    private static string? ExtractJsonObject(string raw)
    {
        var first = raw.IndexOf('{'); var last = raw.LastIndexOf('}');
        return first >= 0 && last > first ? raw[first..(last + 1)] : null;
    }

    private const string SystemPrompt = """
        You reformat a raw, machine-generated meeting transcript into clean, readable paragraphs.
        Input: line-by-line text prefixed with speaker ([You] or [Others]).
        Output ONLY valid JSON: {"paragraphs":[{"speaker":"you","text":"..."},{"speaker":"others","text":"..."}]}
        Rules: merge consecutive lines from the same speaker; fix punctuation; start a new paragraph
        on every speaker change; preserve the original language; remove filler words.
        """;
}
