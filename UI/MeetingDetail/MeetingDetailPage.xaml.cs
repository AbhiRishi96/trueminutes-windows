using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using TrueMinutes.Windows.App;
using TrueMinutes.Windows.Store;
using TrueMinutes.Windows.UI.Shell;

namespace TrueMinutes.Windows.UI.MeetingDetail;

public sealed partial class MeetingDetailPage : Page
{
    private static readonly AppState State = App.State;
    private MeetingRecord? _meeting;
    private string _currentTab = "Summary";

    public MeetingDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is string meetingId)
            Load(meetingId);
    }

    private void Load(string meetingId)
    {
        var repo = new MeetingRepository();
        _meeting = repo.GetMeeting(meetingId);
        if (_meeting == null) return;

        TitleBox.Text   = _meeting.Title;
        DateLabel.Text  = _meeting.FormattedDate;

        if (!string.IsNullOrEmpty(_meeting.CategoryDisplay))
        {
            CategoryPill.Visibility  = Visibility.Visible;
            CategoryLabel.Text       = _meeting.CategoryDisplay;
        }

        ShowSummaryTab();
    }

    private void OnTabChanged(object sender, RoutedEventArgs e)
    {
        if (SummaryTab.IsChecked    == true) ShowSummaryTab();
        else if (TranscriptTab.IsChecked == true) ShowTranscriptTab();
        else if (NotesTab.IsChecked == true) ShowNotesTab();
    }

    private void ShowSummaryTab()
    {
        _currentTab = "Summary";
        SummaryContent.Visibility = Visibility.Visible;
        // TODO: load summary from DB
        SummaryText.Text = _meeting == null
            ? "No summary yet."
            : $"Summary for: {_meeting.Title}\n\n(Configure Settings → Summarization to enable AI summaries.)";
    }

    private void ShowTranscriptTab()
    {
        _currentTab = "Transcript";
        SummaryContent.Visibility = Visibility.Collapsed;
        // TODO: render formatted paragraphs with Clean/Raw toggle
    }

    private void ShowNotesTab()
    {
        _currentTab = "Notes";
        SummaryContent.Visibility = Visibility.Collapsed;
        // TODO: render editable markdown notes
    }

    private void OnBack(object sender, RoutedEventArgs e) => Frame.GoBack();

    private void OnTitleChanged(object sender, RoutedEventArgs e)
    {
        // TODO: persist updated title to DB
    }

    private void OnRegenerate(object sender, RoutedEventArgs e)
    {
        // TODO: trigger SummarizationService.regenerateSummary
    }

    private void OnCopySummary(object sender, RoutedEventArgs e)
    {
        var dp = new Windows.ApplicationModel.DataTransfer.DataPackage();
        dp.SetText(SummaryText.Text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(dp);
    }

    private void OnCopyTranscript(object sender, RoutedEventArgs e) { }
    private void OnExportPdf(object sender, RoutedEventArgs e) { }
    private void OnExportNotion(object sender, RoutedEventArgs e) { }
}
