using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using TrueMinutes.Windows.App;
using TrueMinutes.Windows.UI.LiveTranscript;
using TrueMinutes.Windows.UI.MeetingDetail;
using TrueMinutes.Windows.UI.Settings;

namespace TrueMinutes.Windows.UI.Shell;

public sealed partial class MainShellPage : UserControl
{
    private static readonly AppState State = TrueMinutesApp.State;

    public MainShellPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        State.PropertyChanged += OnStateChanged;
        Navigate("Meetings");
        NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>()
            .FirstOrDefault(i => (string?)i.Tag == "Meetings");
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateStatusUI();
    }

    private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item && item.Tag is string tag)
            Navigate(tag);
    }

    private void Navigate(string tag)
    {
        var pageType = tag switch
        {
            "Live"     => typeof(LiveTranscriptPage),
            "Meetings" => typeof(MeetingListPage),
            "Ask"      => typeof(AskPage),
            "Settings" => typeof(SettingsPage),
            _          => typeof(MeetingListPage)
        };
        ContentFrame.Navigate(pageType);
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AppState.Status) or nameof(AppState.StatusLabel)
            or nameof(AppState.IsRecording))
        {
            UpdateStatusUI();
        }

        if (e.PropertyName == nameof(AppState.IsRecording))
        {
            LiveNavItem.Visibility = State.IsRecording ? Visibility.Visible : Visibility.Collapsed;
            if (State.IsRecording)
            {
                // Auto-navigate to Live while recording.
                NavView.SelectedItem = LiveNavItem;
                Navigate("Live");
            }
            else if (ContentFrame.CurrentSourcePageType == typeof(LiveTranscriptPage))
            {
                NavView.SelectedItem = NavView.MenuItems.OfType<NavigationViewItem>()
                    .FirstOrDefault(i => (string?)i.Tag == "Meetings");
                Navigate("Meetings");
            }
        }
    }

    private void UpdateStatusUI()
    {
        StatusLabel.Text = State.StatusLabel;
        StatusDot.Fill = State.Status switch
        {
            AppState.AppStatus.Recording   => new SolidColorBrush(Colors.Red),
            AppState.AppStatus.Detecting   => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 110, 86, 247)),
            AppState.AppStatus.Summarizing => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 47, 128, 237)),
            AppState.AppStatus.Stopping    => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 217, 119, 6)),
            AppState.AppStatus.Error       => new SolidColorBrush(Colors.Red),
            _                              => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 115, 115, 112))
        };
    }
}
