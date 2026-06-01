using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace TrueMinutes.Windows.UI.Shell;

public sealed partial class AskPage : Page
{
    public AskPage() => InitializeComponent();

    private async void OnAsk(object sender, RoutedEventArgs e)
    {
        var q = QuestionBox.Text.Trim();
        if (string.IsNullOrEmpty(q)) return;
        AnswerText.Text = "Searching…";
        // TODO: wire to AskService (FTS search over transcript_segment)
        await Task.Delay(300);
        AnswerText.Text = "Full-text search across your meeting transcripts is coming. Make sure you have recorded at least one meeting.";
    }
}
