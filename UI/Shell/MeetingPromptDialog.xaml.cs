using Microsoft.UI.Xaml.Controls;
using TrueMinutes.Windows.Detect;

namespace TrueMinutes.Windows.UI.Shell;

/// Meeting detection prompt — Windows equivalent of macOS MeetingPromptPanelController.
/// Uses ContentDialog (modal) which is simpler than a custom NSPanel on Windows.
public sealed class MeetingPromptDialog : ContentDialog
{
    public MeetingPromptDialog(MeetingSignal signal)
    {
        Title = $"Meeting detected in {signal.Platform.DisplayName()}";

        var countdown = new StackPanel { Spacing = 8 };
        countdown.Children.Add(new TextBlock
        {
            Text = "TrueMinutes detected an active meeting. Start recording now?",
            TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap,
            FontSize = 14
        });

        if (!string.IsNullOrEmpty(signal.MeetingTitle))
        {
            countdown.Children.Add(new TextBlock
            {
                Text = signal.MeetingTitle,
                FontSize = 13,
                Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                    Windows.UI.Color.FromArgb(255, 168, 168, 164))
            });
        }

        countdown.Children.Add(new TextBlock
        {
            Text = "Recording stays on your device. Nothing joins the call.",
            FontSize = 11,
            Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 115, 115, 112))
        });

        Content = countdown;
        PrimaryButtonText   = "Start recording";
        SecondaryButtonText = "Skip";
        DefaultButton = ContentDialogButton.Primary;
    }
}
