using Whisper.net.Ggml;

namespace TrueMinutes.Windows.Transcribe;

/// Persisted transcription preferences — Windows equivalent of macOS TranscriptionSettings.
/// Stored in %APPDATA%\TrueMinutes\settings.json (or Windows registry for simpler primitives).
public static class TranscriptionSettings
{
    // Mirrors macOS chunk range (10–30 s, default 15 s).
    public const double MinChunkSeconds = 10.0;
    public const double MaxChunkSeconds = 30.0;
    public const double DefaultChunkSeconds = 15.0;
    public const double DefaultOverlapSeconds = 0.4;

    public static double ChunkDurationSeconds
    {
        get => GetDouble("ChunkDurationSeconds", DefaultChunkSeconds);
        set => SetDouble("ChunkDurationSeconds", Math.Clamp(value, MinChunkSeconds, MaxChunkSeconds));
    }

    public static GgmlType WhisperModel
    {
        get => Enum.TryParse<GgmlType>(GetString("WhisperModel", "SmallEn"), out var v) ? v : GgmlType.SmallEn;
        set => SetString("WhisperModel", value.ToString());
    }

    public static string LanguageMode
    {
        get => GetString("LanguageMode", "translateToEnglish");
        set => SetString("LanguageMode", value);
    }

    // ---- Public string read/write (used by SettingsPage) ----

    public static string GetString(string key, string @default) =>
        Cache.TryGetValue(key, out var v) ? v : @default;

    public static void SetString(string key, string value) { Cache[key] = value; Save(); }

    // ---- simple file-backed store (replace with Windows registry or EF Core UserSettings) ----
    // NOTE: GetString/SetString are public above; GetDouble/SetDouble/Save/Cache remain private.

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TrueMinutes", "settings.json");

    private static Dictionary<string, string>? _cache;

    private static Dictionary<string, string> Cache
    {
        get
        {
            if (_cache != null) return _cache;
            try
            {
                if (File.Exists(SettingsPath))
                {
                    var json = File.ReadAllText(SettingsPath);
                    _cache = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? [];
                }
            }
            catch { }
            return _cache ??= [];
        }
    }

    private static void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, System.Text.Json.JsonSerializer.Serialize(Cache));
        }
        catch { }
    }

    private static double GetDouble(string key, double @default) =>
        Cache.TryGetValue(key, out var v) && double.TryParse(v, out var d) ? d : @default;

    private static void SetDouble(string key, double value) => SetString(key, value.ToString("R"));
}
