using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace TrueMinutes.Windows.Capture;

/// Captures microphone input via WASAPI — the Windows equivalent of macOS AVAudioEngine
/// with an installTap on the input node. Delivers mono float32 PCM at 16 kHz to an AudioChunker.
public sealed class MicCapturer : IDisposable
{
    private WasapiCapture? _capture;
    private readonly AudioChunker _chunker;
    private bool _running;

    public AudioChunker Chunker => _chunker;

    public event Action<float>? RmsReported;

    public MicCapturer(double chunkSeconds = 15.0, double overlapSeconds = 0.4)
    {
        _chunker = new AudioChunker(AudioSource.Mic, chunkSeconds, overlapSeconds);
    }

    public void Start()
    {
        if (_running) return;
        _running = true;

        // Use default communications device (what meeting apps use)
        var enumerator = new MMDeviceEnumerator();
        var device = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        _capture = new WasapiCapture(device);
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
        var fmt = _capture!.WaveFormat;
        var pcm = ConvertToMono16k(e.Buffer, e.BytesRecorded, fmt);
        if (pcm.Length == 0) return;

        float rms = QuickRms(pcm);
        if (rms > 0.001f) RmsReported?.Invoke(rms);

        _chunker.Append(pcm);
    }

    private static float[] ConvertToMono16k(byte[] buffer, int byteCount, WaveFormat fmt)
    {
        float[] floats;

        // Handle PCM 16-bit (most common mic format)
        if (fmt.Encoding == WaveFormatEncoding.Pcm && fmt.BitsPerSample == 16)
        {
            int samples = byteCount / 2;
            floats = new float[samples];
            for (int i = 0; i < samples; i++)
                floats[i] = BitConverter.ToInt16(buffer, i * 2) / 32768f;
        }
        else if (fmt.Encoding == WaveFormatEncoding.IeeeFloat)
        {
            int samples = byteCount / 4;
            floats = new float[samples];
            for (int i = 0; i < samples; i++)
                floats[i] = BitConverter.ToSingle(buffer, i * 4);
        }
        else
        {
            return []; // unsupported — log in production
        }

        // Downmix to mono if stereo
        int ch = fmt.Channels;
        float[] mono;
        if (ch == 1)
        {
            mono = floats;
        }
        else
        {
            int monoLen = floats.Length / ch;
            mono = new float[monoLen];
            for (int i = 0; i < monoLen; i++)
            {
                float s = 0f;
                for (int c = 0; c < ch; c++) s += floats[i * ch + c];
                mono[i] = s / ch;
            }
        }

        // Resample to 16 kHz
        if (fmt.SampleRate == 16_000) return mono;
        double ratio = fmt.SampleRate / 16_000.0;
        int outLen = (int)(mono.Length / ratio);
        var out16 = new float[outLen];
        for (int j = 0; j < outLen; j++)
        {
            double idx = j * ratio;
            int lo = (int)idx, hi = Math.Min(lo + 1, mono.Length - 1);
            out16[j] = (float)(mono[lo] * (1 - idx + lo) + mono[hi] * (idx - lo));
        }
        return out16;
    }

    private static float QuickRms(float[] s)
    {
        if (s.Length == 0) return 0f;
        float sum = 0f; int step = Math.Max(1, s.Length / 16), n = 0;
        for (int i = 0; i < s.Length; i += step) { sum += s[i] * s[i]; n++; }
        return n > 0 ? MathF.Sqrt(sum / n) : 0f;
    }

    public void Dispose() { Stop(); _capture?.Dispose(); }
}
