namespace TrueMinutes.Windows.Capture;

/// Audio source — mirrors the macOS AudioSource enum.
public enum AudioSource { Mic, System }

/// A fixed-size chunk of PCM float32 samples at 16 kHz mono, ready for Whisper.
/// Direct port of the macOS AudioChunkItem struct.
public sealed record AudioChunkItem(
    float[] Samples,
    int StartMs,          // wall-clock offset from recording start (milliseconds)
    AudioSource Source);
