using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrueMinutes.Windows.App;
using TrueMinutes.Windows.Summarize;

namespace TrueMinutes.Windows.UI.LiveTranscript;

public sealed partial class LiveTranscriptPage : Page
{
    private static readonly AppState State = TrueMinutesApp.State;
    private DispatcherTimer? _timer;

    public LiveTranscriptPage()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Bind to live store.
        MeetingTitleText.Text = State.LiveStore.MeetingTitle;

        State.LiveStore.Paragraphs.CollectionChanged += (_, _) => RenderParagraphs();
        State.LiveStore.Segments.CollectionChanged   += (_, _) => UpdateTail();

        RenderParagraphs();
        UpdateTail();

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsed();
        _timer.Start();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) => _timer?.Stop();

    private void UpdateElapsed()
    {
        var s = State.LiveStore.ElapsedSeconds;
        ElapsedText.Text = s >= 3600
            ? $"{s/3600}:{(s%3600)/60:D2}:{s%60:D2}"
            : $"{s/60}:{s%60:D2}";
    }

    /// Render reflowed paragraphs — mirrors macOS LiveTranscriptPanel paragraph list.
    private void RenderParagraphs()
    {
        ParagraphsPanel.Children.Clear();
        SpeakerParagraph? prev = null;

        foreach (var para in State.LiveStore.Paragraphs)
        {
            var showTurnCue = prev == null || prev.TurnKey != para.TurnKey;
            ParagraphsPanel.Children.Add(BuildParagraphBlock(para, showTurnCue));
            prev = para;
        }

        // Auto-scroll to bottom.
        _ = DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(50);
            TranscriptScroll.ChangeView(null, TranscriptScroll.ScrollableHeight, null);
        });
    }

    /// Show the newest raw segment text as the dim live "tail".
    private void UpdateTail()
    {
        var segs = State.LiveStore.Segments;
        if (segs.Count == 0) { LiveTailText.Text = "Listening…"; return; }
        var last = segs[^1];
        LiveTailText.Text = last.Text;
    }

    private static UIElement BuildParagraphBlock(SpeakerParagraph para, bool showTurnCue)
    {
        var outer = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 4) };

        // Subtle turn cue — only when speaker changes.
        if (showTurnCue)
        {
            outer.Children.Add(new TextBlock
            {
                Text = para.DisplaySpeaker.ToUpperInvariant(),
                FontSize = 10,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                CharacterSpacing = 100,
                Foreground = new SolidColorBrush(para.Speaker == "you"
                    ? global::Windows.UI.Color.FromArgb(255, 110, 86, 247)
                    : global::Windows.UI.Color.FromArgb(255, 47, 128, 237)),
                Margin = new Thickness(0, 8, 0, 0)
            });
        }

        outer.Children.Add(new TextBlock
        {
            Text = para.Text,
            FontSize = 14,
            Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 242, 242, 240)),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 22,
            IsTextSelectionEnabled = true
        });

        return outer;
    }

    private void OnStopRecording(object sender, RoutedEventArgs e)
    {
        _ = State.StopRecordingAsync();
    }
}
