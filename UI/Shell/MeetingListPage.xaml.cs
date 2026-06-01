using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using TrueMinutes.Windows.App;
using TrueMinutes.Windows.Store;
using TrueMinutes.Windows.UI.MeetingDetail;

namespace TrueMinutes.Windows.UI.Shell;

public sealed partial class MeetingListPage : Page
{
    private static readonly AppState State = TrueMinutesApp.State;
    private string? _categoryFilter;

    public MeetingListPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        State.Meetings.CollectionChanged += (_, _) => RenderMeetings();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        State.RefreshMeetings();
        BuildFilterChips();
        RenderMeetings();
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        RenderMeetings(sender.Text);
    }

    private void BuildFilterChips()
    {
        FilterChipPanel.Children.Clear();
        var categories = new[] { ("All", (string?)null), ("1:1", "one_on_one"), ("Team", "team"), ("Customer", "customer"), ("Standup", "standup"), ("Review", "review") };
        foreach (var (label, key) in categories)
        {
            var chip = new Button
            {
                Content = label,
                Tag = key,
                Margin = new Thickness(0),
                Padding = new Thickness(10, 5, 10, 5),
                CornerRadius = new CornerRadius(999),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.Medium,
                Background = key == _categoryFilter
                    ? new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 110, 86, 247))
                    : new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 26, 26, 38)),
                Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 242, 242, 240)),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 51, 51, 68)),
            };
            chip.Click += (_, _) =>
            {
                _categoryFilter = key;
                BuildFilterChips();
                RenderMeetings(SearchBox.Text);
            };
            FilterChipPanel.Children.Add(chip);
        }
    }

    private void RenderMeetings(string? query = null)
    {
        MeetingListPanel.Children.Clear();

        var meetings = State.Meetings.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(query))
            meetings = meetings.Where(m => m.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (_categoryFilter != null)
            meetings = meetings.Where(m => m.AutoCategory == _categoryFilter);

        var list = meetings.ToList();
        EmptyState.Visibility = list.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        CountLabel.Text = $"{list.Count} meetings";

        foreach (var meeting in list)
            MeetingListPanel.Children.Add(BuildMeetingCard(meeting));
    }

    private UIElement BuildMeetingCard(MeetingRecord meeting)
    {
        var card = new Border
        {
            Style = (Style)Resources["MeetingRowStyle"]
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Left: title + meta
        var left = new StackPanel { Spacing = 4 };

        var title = new TextBlock
        {
            Text = meeting.Title,
            FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 242, 242, 240)),
            TextTrimming = TextTrimming.CharacterEllipsis
        };
        left.Children.Add(title);

        // Date + category pill
        var meta = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        meta.Children.Add(new TextBlock
        {
            Text = meeting.FormattedDate,
            FontSize = 12,
            Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 168, 168, 164))
        });
        if (!string.IsNullOrEmpty(meeting.CategoryDisplay))
        {
            meta.Children.Add(new Border
            {
                Background = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 32, 26, 58)),
                CornerRadius = new CornerRadius(999),
                Padding = new Thickness(8, 2, 8, 2),
                Child = new TextBlock
                {
                    Text = meeting.CategoryDisplay,
                    FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 110, 86, 247))
                }
            });
        }
        left.Children.Add(meta);
        Grid.SetColumn(left, 0);
        grid.Children.Add(left);

        // Right: status chip
        var statusChip = new Border
        {
            Background = StatusBackground(meeting.Status),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(8, 3, 8, 3),
            VerticalAlignment = VerticalAlignment.Center,
            Child = new TextBlock
            {
                Text = StatusLabel(meeting.Status, meeting.SummaryStatus),
                FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Foreground = StatusForeground(meeting.Status)
            }
        };
        Grid.SetColumn(statusChip, 1);
        grid.Children.Add(statusChip);

        card.Child = grid;

        // Click → open detail
        card.PointerPressed += (_, _) =>
        {
            State.SelectedMeetingId = meeting.Id;
            Frame.Navigate(typeof(MeetingDetailPage), meeting.Id);
        };

        return card;
    }

    private static SolidColorBrush StatusBackground(string status) => status switch
    {
        "recording"  => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 59, 28, 28)),
        "ready"      => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 22, 41, 30)),
        "failed"     => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 59, 23, 21)),
        _            => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 34, 34, 46))
    };

    private static SolidColorBrush StatusForeground(string status) => status switch
    {
        "recording" => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 224, 62, 62)),
        "ready"     => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 15, 138, 78)),
        "failed"    => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 180, 35, 24)),
        _           => new SolidColorBrush(global::Windows.UI.Color.FromArgb(255, 168, 168, 164))
    };

    private static string StatusLabel(string status, string? summaryStatus) => status switch
    {
        "recording" => "Recording",
        "ready"     => summaryStatus == "failed" ? "No summary" : "Ready",
        "failed"    => "Failed",
        _           => status
    };
}
