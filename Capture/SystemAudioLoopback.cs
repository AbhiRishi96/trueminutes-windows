using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TrueMinutes.Windows.Capture;

/// Captures system audio output via WASAPI loopback — the Windows equivalent of
/// macOS AudioHardwareCreateProcessTap / CATapDescription.
///
/// WASAPI loopback taps the default render (speaker/headphone) endpoint as a virtual
/// input device, giving us all audio that plays through the speakers: meeting call audio,
/// notifications, etc. DRM-protected content (Netflix, Spotify) is automatically excluded
/// by the OS audio graph.
///
/// Output: mono float32 PCM at 16 kHz (Whisper's required format).
public sealed class SystemAudioLoopback : IDisposable
{
    private WasapiLoopbackCapture? _capture;
    private readonly AudioChunker _chunker;
    private bool _running;

    public AudioChunker Chunker => _chunker;

    /// Fired when a meaningful audio level is detected (for energy-based VAD / silence monitoring).
    public event Action<float>? RmsReported;

    public SystemAudioLoopback(double chunkSeconds = 15.0, double overlapSeconds = 0.4)
    {
        _chunker = new AudioChunker(AudioSource.System, chunkSeconds, overlapSeconds);
    }

    /// Start capturing the default speaker output.
    public void Start()
    {
        if (_running) return;
        _running = true;

        _capture = new WasapiLoopbackCapture();
        _capture.DataAvailable += OnDataAvailable;
        _capture.RecordingStopped += (_, _) => _running = false;
        _capture.StartRecording();
    }

    public void Stop()
    {
        if (!_running) return;
        _capture?.StopRecording();
        _chunker.Flush();
        _running = false;
    }

    private void OnDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0) return;

        // WASAPI loopback delivers IEEE float in the device's native format (usually 32-bit float,
        // stereo, 44100/48000 Hz). Resample to mono 16kHz float32 for Whisper.
        var native = _capture!.WaveFormat;
        var pcm = ResampleToMono16k(e.Buffer, e.BytesRecorded, native);
        if (pcm.Length == 0) return;

        // Energy check — log RMS for silence detection (same as macOS noteAudio).
        float rms = QuickRms(pcm);
        if (rms > 0.001f) RmsReported?.Invoke(rms);

        _chunker.Append(pcm);
    }

    /// Naive but accurate resampler: convert stereo/multi-channel IEEE float to mono 16kHz float32.
    /// For production, replace with NAudio's MediaFoundationResampler for better quality.
    private static float[] ResampleToMono16k(byte[] buffer, int byteCount, WaveFormat fmt)
    {
        if (fmt.Encoding != WaveFormatEncoding.IeeeFloat)
            return []; // unexpected format — skip; log in production

        int channels = fmt.Channels;
        int srcRate = fmt.SampleRate;
        int sampleCount = byteCount / (4 * channels); // 4 bytes per float32

        // --- Step 1: interleaved → mono by averaging channels ---
        var mono = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            float sum = 0f;
            for (int c = 0; c < channels; c++)
                sum += BitConverter.ToSingle(buffer, (i * channels + c) * 4);
            mono[i] = sum / channels;
        }

        if (srcRate == 16_000) return mono;

        // --- Step 2: simple linear interpolation to 16 kHz ---
        double ratio = srcRate / 16_000.0;
        int outLen = (int)(sampleCount / ratio);
        var resampled = new float[outLen];
        for (int j = 0; j < outLen; j++)
        {
            double srcIdx = j * ratio;
            int lo = (int)srcIdx;
            int hi = Math.Min(lo + 1, sampleCount - 1);
            double t = srcIdx - lo;
            resampled[j] = (float)(mono[lo] * (1 - t) + mono[hi] * t);
        }
        return resampled;
    }

    private static float QuickRms(float[] samples)
    {
        if (samples.Length == 0) return 0f;
        float sum = 0f;
        int step = Math.Max(1, samples.Length / 16);
        int count = 0;
        for (int i = 0; i < samples.Length; i += step) { sum += samples[i] * samples[i]; count++; }
        return count > 0 ? MathF.Sqrt(sum / count) : 0f;
    }

    public void Dispose()
    {
        Stop();
        _capture?.Dispose();
    }
}
