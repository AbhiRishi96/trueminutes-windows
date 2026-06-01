using TrueMinutes.Windows.Capture;

namespace TrueMinutes.Windows.Transcribe;

/// Contract for all ASR backends — mirrors the macOS Transcriber protocol.
public interface ITranscriber : IAsyncDisposable
{
    string DisplayName { get; }

    /// Transcribe a chunk of mono float32 PCM at 16 kHz.
    Task<TranscriptSegment> TranscribeAsync(
        float[] audio,
        int sampleRateHz,
        AudioSource source,
        string? speakerLabel,
        CancellationToken ct = default);
}

public sealed record TranscriptSegment(
    string Id,
    int StartMs,
    int EndMs,
    AudioSource Source,
    string? SpeakerLabel,
    string Text);
