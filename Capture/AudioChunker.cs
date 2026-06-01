using System.Threading.Channels;

namespace TrueMinutes.Windows.Capture;

/// Accumulates raw PCM float32 samples (16 kHz mono) and emits fixed-duration chunks with
/// a short overlap for boundary-word continuity. Direct port of macOS AudioChunker.swift.
///
/// Design: push samples via Append(); fully emitted chunks are written to an output Channel
/// so the consumer runs on its own task without blocking the capture callback.
public sealed class AudioChunker
{
    private readonly int _sampleRate;
    private readonly int _chunkSamples;     // samples per chunk
    private readonly int _overlapSamples;   // samples reused from previous chunk
    private readonly AudioSource _source;
    private readonly Channel<AudioChunkItem> _channel;
    private readonly List<float> _buffer = [];
    private int _startMs;           // wall-clock start of the next chunk
    private int _totalSamplesIn;    // total samples received since recording started

    public ChannelReader<AudioChunkItem> Output => _channel.Reader;

    /// <param name="chunkSeconds">Length of each chunk in seconds (plan default: 15 s).</param>
    /// <param name="overlapSeconds">Overlap shared between consecutive chunks (default: 0.4 s).</param>
    /// <param name="source">Labels every emitted chunk.</param>
    /// <param name="sampleRate">Must be 16000 Hz — Whisper's required rate.</param>
    public AudioChunker(
        AudioSource source,
        double chunkSeconds = 15.0,
        double overlapSeconds = 0.4,
        int sampleRate = 16_000,
        int capacity = 8)
    {
        _source = source;
        _sampleRate = sampleRate;
        _chunkSamples = (int)(chunkSeconds * sampleRate);
        _overlapSamples = Math.Min((int)(overlapSeconds * sampleRate), _chunkSamples - 1);
        _channel = Channel.CreateBounded<AudioChunkItem>(new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false
        });
    }

    /// Append samples from the capture callback (called on audio thread).
    public void Append(float[] samples)
    {
        lock (_buffer)
        {
            _buffer.AddRange(samples);
            _totalSamplesIn += samples.Length;

            while (_buffer.Count >= _chunkSamples)
                EmitChunkLocked();
        }
    }

    /// Flush remaining samples as a final partial chunk (called on recording stop).
    public void Flush()
    {
        lock (_buffer)
        {
            if (_buffer.Count > 0)
                EmitChunkLocked(partial: true);
        }
        _channel.Writer.TryComplete();
    }

    // Must be called with _buffer lock held.
    private void EmitChunkLocked(bool partial = false)
    {
        var count = partial ? _buffer.Count : _chunkSamples;
        if (count == 0) return;

        var chunk = _buffer.GetRange(0, count).ToArray();
        var item = new AudioChunkItem(chunk, _startMs, _source);

        // Non-blocking send; DropOldest handles back-pressure.
        _channel.Writer.TryWrite(item);

        // Advance: slide window forward by (chunkSize - overlap)
        var advance = partial ? count : (_chunkSamples - _overlapSamples);
        _buffer.RemoveRange(0, Math.Min(advance, _buffer.Count));
        _startMs += (int)((double)advance / _sampleRate * 1000.0);
    }
}
