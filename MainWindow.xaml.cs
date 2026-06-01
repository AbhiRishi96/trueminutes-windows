using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using TrueMinutes.Windows.App;
using TrueMinutes.Windows.UI.FloatingPill;
using Windows.Graphics;

namespace TrueMinutes.Windows;

public sealed partial class MainWindow : Window
{
    private static readonly AppState State = App.State;
    private FloatingPillWindow? _floatingPill;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;

        // Resize + centre on screen.
        AppWindow.Resize(new SizeInt32(1100, 720));
        CentreWindow();

        // Dark title bar.
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var tb = AppWindow.TitleBar;
            tb.ExtendsContentIntoTitleBar = true;
            tb.ButtonBackgroundColor         = Colors.Transparent;
            tb.ButtonForegroundColor         = Windows.UI.Color.FromArgb(255, 242, 242, 240);
            tb.ButtonInactiveBackgroundColor = Colors.Transparent;
            tb.ButtonHoverBackgroundColor    = Windows.UI.Color.FromArgb(30, 255, 255, 255);
        }

        // Wire recording state to mini-bar + floating pill.
        State.RecordingStarted += OnRecordingStarted;
        State.RecordingStopped += OnRecordingStopped;
        State.PropertyChanged  += (_, e) =>
        {
            if (e.PropertyName == nameof(AppState.IsRecording))
                MiniBar.Visibility = State.IsRecording ? Visibility.Visible : Visibility.Collapsed;
        };

        // Wire meeting-detected event → prompt dialog.
        State.MeetingDetected += OnMeetingDetected;
    }

    private void OnRecordingStarted(Detect.MeetingSignal signal)
    {
        MiniBar.Visibility = Visibility.Visible;
        _floatingPill ??= new FloatingPillWindow();
        _floatingPill.Show(signal.MeetingTitle ?? signal.Platform.DisplayName());
    }

    private void OnRecordingStopped()
    {
        MiniBar.Visibility = Visibility.Collapsed;
        _floatingPill?.Close();
        _floatingPill = null;
    }

    private async void OnMeetingDetected(Detect.MeetingSignal signal)
    {
        var dlg = new UI.Shell.MeetingPromptDialog(signal) { XamlRoot = Content.XamlRoot };
        var result = await dlg.ShowAsync();
        if (result == ContentDialogResult.Primary)
            State.UserAcceptedMeeting(signal);
        else
            State.UserDeclinedMeeting();
    }

    public void BringToFront()
    {
        AppWindow.Show();
        Activate();
    }

    private void CentreWindow()
    {
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        var x = (area.WorkArea.Width  - 1100) / 2;
        var y = (area.WorkArea.Height - 720)  / 2;
        AppWindow.Move(new PointInt32(Math.Max(0, x), Math.Max(0, y)));
    }
}
