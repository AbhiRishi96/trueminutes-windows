using TrueMinutes.Windows.Capture;
using Whisper.net;
using Whisper.net.Ggml;

namespace TrueMinutes.Windows.Transcribe;

/// ASR backend using Whisper.net (wraps whisper.cpp). Windows equivalent of macOS WhisperKitTranscriber.
///
/// Model acceleration order (mirrors macOS ANE→GPU→CPU):
///   1. CUDA (Nvidia)  — add Whisper.net.Runtime.Cuda NuGet + set RuntimeType.Cuda
///   2. DirectML       — any DirectX 12 GPU (Intel/AMD/Nvidia on Win10+)
///   3. CPU            — always available (fallback)
///
/// Models are downloaded from Hugging Face on first use into %LOCALAPPDATA%\TrueMinutes\whisper-models.
/// Available: Tiny (39M), Base (74M), Small (244M, recommended), Medium (769M), LargeV3Turbo (809M).
public sealed class WhisperNetTranscriber : ITranscriber
{
    private readonly WhisperFactory _factory;
    private readonly WhisperProcessor _processor;
    private readonly string _modelName;

    public string DisplayName => $"Whisper.net ({_modelName})";

    private WhisperNetTranscriber(WhisperFactory factory, WhisperProcessor processor, string modelName)
    {
        _factory = factory;
        _processor = processor;
        _modelName = modelName;
    }

    /// Load (or download) a Whisper model. Equivalent to WhisperKitTranscriber.init(model:).
    public static async Task<WhisperNetTranscriber> CreateAsync(
        GgmlType modelType = GgmlType.SmallEn,
        string? modelDir = null,
        CancellationToken ct = default)
    {
        var dir = modelDir ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrueMinutes", "whisper-models");
        Directory.CreateDirectory(dir);

        var modelFile = Path.Combine(dir, $"ggml-{ModelFileName(modelType)}.bin");
        if (!File.Exists(modelFile))
        {
            Console.WriteLine($"Downloading Whisper model {modelType}…");
            await using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(
                modelType,
                QuantizationType.NoQuantization,
                ct);
            await using var fs = File.Create(modelFile);
            await modelStream.CopyToAsync(fs, ct);
        }

        var factory = WhisperFactory.FromPath(modelFile);
        var processor = factory.CreateBuilder()
            .WithLanguage("auto")       // detect language; set "en" for English-only models
            .WithTranslate()            // translate non-English to English (matches macOS translate task)
            .WithNoContext()
            .WithTemperature(0f)
            .Build();

        return new WhisperNetTranscriber(factory, processor, ModelFileName(modelType));
    }

    public async Task<TranscriptSegment> TranscribeAsync(
        float[] audio,
        int sampleRateHz,
        AudioSource source,
        string? speakerLabel,
        CancellationToken ct = default)
    {
        if (audio.Length == 0)
            return new TranscriptSegment(Guid.NewGuid().ToString(), 0, 0, source, speakerLabel, "");

        var parts = new List<string>();
        await foreach (var segment in _processor.ProcessAsync(audio, ct))
        {
            var text = segment.Text.Trim();
            if (!string.IsNullOrWhiteSpace(text))
                parts.Add(text);
        }

        var joined = string.Join(" ", parts).Trim();
        var durationMs = (int)((double)audio.Length / sampleRateHz * 1000);
        return new TranscriptSegment(
            Guid.NewGuid().ToString(),
            StartMs: 0,
            EndMs: durationMs,
            source,
            speakerLabel,
            Text: joined);
    }

    private static string ModelFileName(GgmlType type) => type switch
    {
        GgmlType.Tiny       => "tiny",
        GgmlType.TinyEn     => "tiny.en",
        GgmlType.Base       => "base",
        GgmlType.BaseEn     => "base.en",
        GgmlType.Small      => "small",
        GgmlType.SmallEn    => "small.en",
        GgmlType.Medium     => "medium",
        GgmlType.MediumEn   => "medium.en",
        GgmlType.LargeV3    => "large-v3",
        GgmlType.LargeV3Turbo => "large-v3-turbo",
        _ => type.ToString().ToLowerInvariant()
    };

    public async ValueTask DisposeAsync()
    {
        _processor.Dispose();
        _factory.Dispose();
        await Task.CompletedTask;
    }
}
