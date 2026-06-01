using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrueMinutes.Windows.App;
using TrueMinutes.Windows.Security;
using TrueMinutes.Windows.Summarize;
using TrueMinutes.Windows.Transcribe;
using Whisper.net.Ggml;

namespace TrueMinutes.Windows.UI.Settings;

public sealed partial class SettingsPage : Page
{
    private static readonly AppState State = App.State;
    private bool _loading = true;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _loading = true;

        // Profile
        DisplayNameBox.Text = TranscriptionSettings.GetString("DisplayName", Environment.UserName);

        // Language mode
        LanguageModeBox.SelectedIndex = TranscriptionSettings.LanguageMode switch
        {
            "english" => 1, "hinglish" => 2, _ => 0
        };

        // Chunk slider
        ChunkSlider.Value = TranscriptionSettings.ChunkDurationSeconds;
        ChunkLabel.Text = $"{(int)TranscriptionSettings.ChunkDurationSeconds}s";

        // Summarizer
        var engine = TranscriptionSettings.GetString("SummarizerEngine", "ollama");
        SummarizerBox.SelectedIndex = engine switch
        {
            "openai" => 1, "anthropic" => 2, "groq" => 3, "openrouter" => 4, _ => 0
        };
        OllamaModelBox.Text = TranscriptionSettings.GetString("OllamaModel", "qwen2.5:7b");
        UpdateSummarizerUI(engine);

        // Theme
        ThemeBox.SelectedIndex = TranscriptionSettings.GetString("Theme", "dark") switch
        {
            "light" => 1, "system" => 2, _ => 0
        };

        // Hardware summary
        HardwareSummary.Text = $"· {Environment.ProcessorCount} cores";

        BuildModelList();
        _loading = false;
    }

    // ── Whisper model list ──────────────────────────────────────────────────
    private void BuildModelList()
    {
        ModelList.Items.Clear();
        var models = new[]
        {
            (GgmlType.TinyEn,        "Tiny",           "~40 MB",  "Fastest, English only"),
            (GgmlType.BaseEn,        "Base",           "~75 MB",  "Better than Tiny, English only"),
            (GgmlType.SmallEn,       "Small",          "~245 MB", "Recommended — best accuracy/speed balance"),
            (GgmlType.LargeV3Turbo,  "Large v3 Turbo", "~810 MB", "Multilingual, translate-to-English, Hindi/Hinglish"),
        };
        var current = TranscriptionSettings.WhisperModel;
        foreach (var (type, name, size, desc) in models)
        {
            var isSelected = type == current;
            var downloaded = IsModelDownloaded(type);
            ModelList.Items.Add(BuildModelRow(type, name, size, desc, isSelected, downloaded));
        }
    }

    private UIElement BuildModelRow(GgmlType type, string name, string size, string desc,
                                    bool isSelected, bool downloaded)
    {
        var grid = new Grid { Margin = new Thickness(0, 4, 0, 4) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var left = new StackPanel { Spacing = 2 };
        left.Children.Add(new TextBlock
        {
            Text = name, FontSize = 14, FontWeight = Microsoft.UI.Text.FontWeights.Medium,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 242, 242, 240))
        });
        left.Children.Add(new TextBlock
        {
            Text = $"{size} — {desc}", FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 168, 168, 164))
        });
        Grid.SetColumn(left, 0); grid.Children.Add(left);

        var right = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
                                      VerticalAlignment = VerticalAlignment.Center };
        if (isSelected)
        {
            right.Children.Add(new TextBlock
            {
                Text = "✓ Selected", FontSize = 11, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 15, 138, 78))
            });
        }
        else if (downloaded)
        {
            var useBtn = new Button
            {
                Content = "Use", Padding = new Thickness(10, 4, 10, 4),
                CornerRadius = new CornerRadius(6), FontSize = 11
            };
            useBtn.Click += (_, _) =>
            {
                TranscriptionSettings.WhisperModel = type;
                BuildModelList();
            };
            right.Children.Add(useBtn);
        }
        else
        {
            var dlBtn = new Button
            {
                Content = $"Download · {size}", Padding = new Thickness(10, 4, 10, 4),
                CornerRadius = new CornerRadius(6), FontSize = 11,
                Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 32, 26, 58)),
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 110, 86, 247))
            };
            dlBtn.Click += async (_, _) => await DownloadModelAsync(type, dlBtn);
            right.Children.Add(dlBtn);
        }
        Grid.SetColumn(right, 1); grid.Children.Add(right);

        return new Border
        {
            Child = grid, Padding = new Thickness(12),
            Background = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                isSelected
                    ? Windows.UI.Color.FromArgb(255, 22, 20, 40)
                    : Windows.UI.Color.FromArgb(255, 26, 26, 38)),
            CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 2, 0, 2),
            BorderBrush = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 51, 51, 68)),
            BorderThickness = new Thickness(1)
        };
    }

    private static bool IsModelDownloaded(GgmlType type)
    {
        var name = type.ToString().ToLowerInvariant().Replace("en", ".en");
        var dir = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TrueMinutes", "whisper-models");
        return System.IO.File.Exists(System.IO.Path.Combine(dir, $"ggml-{name}.bin"));
    }

    private async Task DownloadModelAsync(GgmlType type, Button btn)
    {
        btn.IsEnabled = false;
        btn.Content = "Downloading…";
        try
        {
            await WhisperNetTranscriber.CreateAsync(type);
            BuildModelList();
        }
        catch (Exception ex)
        {
            btn.Content = "Failed — retry";
            btn.IsEnabled = true;
            OllamaStatus.Text = ex.Message;
        }
    }

    // ── Event handlers ──────────────────────────────────────────────────────
    private void OnDisplayNameChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            TranscriptionSettings.SetString("DisplayName", DisplayNameBox.Text);
    }

    private void OnLanguageModeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var tag = (LanguageModeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "translateToEnglish";
        TranscriptionSettings.LanguageMode = tag;
    }

    private void OnChunkChanged(object sender, Microsoft.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        ChunkLabel.Text = $"{(int)e.NewValue}s";
        if (!_loading) TranscriptionSettings.ChunkDurationSeconds = e.NewValue;
    }

    private void OnSummarizerChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var tag = (SummarizerBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ollama";
        TranscriptionSettings.SetString("SummarizerEngine", tag);
        UpdateSummarizerUI(tag);
    }

    private void UpdateSummarizerUI(string engine)
    {
        OllamaSection.Visibility  = engine == "ollama" ? Visibility.Visible : Visibility.Collapsed;
        ApiKeySection.Visibility  = engine != "ollama" ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnOllamaModelChanged(object sender, RoutedEventArgs e)
    {
        if (!_loading) TranscriptionSettings.SetString("OllamaModel", OllamaModelBox.Text);
    }

    private async void OnTestOllama(object sender, RoutedEventArgs e)
    {
        OllamaStatus.Text = "Testing…";
        try
        {
            var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var resp = await http.GetAsync($"{OllamaSummarizer.DefaultBaseUrl}/api/tags");
            OllamaStatus.Text = resp.IsSuccessStatusCode ? "✓ Ollama is running" : $"Error {(int)resp.StatusCode}";
        }
        catch
        {
            OllamaStatus.Text = "✗ Ollama not reachable — make sure Ollama is running on port 11434";
        }
    }

    private void OnApiKeyChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var engine = (SummarizerBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "ollama";
        var key = engine switch
        {
            "openai"     => CredentialStore.Key.OpenAIApiKey,
            "anthropic"  => CredentialStore.Key.AnthropicApiKey,
            "groq"       => CredentialStore.Key.GroqApiKey,
            "openrouter" => CredentialStore.Key.OpenRouterApiKey,
            _            => CredentialStore.Key.OllamaBaseUrl
        };
        CredentialStore.Save(key, ApiKeyBox.Password);
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        var tag = (ThemeBox.SelectedItem as ComboBoxItem)?.Tag as string ?? "dark";
        TranscriptionSettings.SetString("Theme", tag);
        // Applying theme requires app restart in WinUI 3.
    }

    private void OnMicToggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
            TranscriptionSettings.SetString("CaptureLocalMic", CaptureLocalMic.IsOn.ToString());
    }

    private void OnLocalOnlyToggled(object sender, RoutedEventArgs e)
    {
        if (!_loading)
        {
            TranscriptionSettings.SetString("LocalOnlyMode", LocalOnlyToggle.IsOn.ToString());
            if (LocalOnlyToggle.IsOn) SummarizerBox.SelectedIndex = 0;
        }
    }
}
