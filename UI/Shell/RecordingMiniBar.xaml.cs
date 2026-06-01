using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrueMinutes.Windows.App;

namespace TrueMinutes.Windows.UI.Shell;

public sealed partial class RecordingMiniBar : UserControl
{
    private static readonly AppState State = TrueMinutesApp.State;
    private DispatcherTimer? _timer;

    public RecordingMiniBar()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateElapsed();
        _timer.Start();
        StartPulse();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _timer?.Stop();
    }

    private void UpdateElapsed()
    {
        var s = State.LiveStore.ElapsedSeconds;
        ElapsedText.Text = s >= 3600
            ? $"{s/3600}:{(s%3600)/60:D2}:{s%60:D2}"
            : $"{s/60}:{s%60:D2}";
    }

    private async void StartPulse()
    {
        while (IsLoaded)
        {
            var anim = new Microsoft.UI.Xaml.Media.Animation.DoubleAnimation
            {
                To = 0.3, Duration = new Duration(TimeSpan.FromMilliseconds(700))
            };
            // Simplified pulse via opacity toggle
            RecordingDot.Opacity = RecordingDot.Opacity > 0.5 ? 0.3 : 1.0;
            await Task.Delay(700);
        }
    }

    private void OnLiveTranscriptClick(object sender, RoutedEventArgs e)
    {
        State.Section = AppState.ShellSection.LiveTranscript;
    }

    private void OnStopClick(object sender, RoutedEventArgs e)
    {
        _ = State.StopRecordingAsync();
    }
}
