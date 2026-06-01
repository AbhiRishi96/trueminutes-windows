using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace TrueMinutes.Windows.UI.FloatingPill;

/// Floating recording indicator — Windows equivalent of macOS NSPanel floating pill.
/// A small, always-on-top window pinned to the top-right of the screen showing
/// recording status, elapsed time, and a stop button. Draggable.
public sealed class FloatingPillWindow : Window
{
    private TextBlock? _timeLabel;
    private DispatcherTimer? _timer;
    private readonly DateTime _startedAt = DateTime.Now;

    public FloatingPillWindow()
    {
        ExtendsContentIntoTitleBar = true;
        AppWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Resize(new SizeInt32(200, 60));

        // Always on top.
        var ovl = AppWindow.Presenter as OverlappedPresenter;
        if (ovl != null)
        {
            ovl.IsAlwaysOnTop   = true;
            ovl.IsResizable     = false;
            ovl.IsMinimizable   = false;
            ovl.IsMaximizable   = false;
            ovl.SetBorderAndTitleBar(false, false);
        }

        // Position top-right.
        var area = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        AppWindow.Move(new PointInt32(area.WorkArea.Width - 210, 20));

        BuildContent();
    }

    private void BuildContent()
    {
        var root = new Border
        {
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(240, 26, 26, 38)),
            CornerRadius = new CornerRadius(999),
            BorderBrush  = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 62, 62)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(16, 0, 12, 0),
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10,
                                    VerticalAlignment = VerticalAlignment.Center, Height = 44 };

        // Pulsing dot
        var dot = new Ellipse { Width = 8, Height = 8,
            Fill = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 62, 62)) };
        row.Children.Add(dot);

        // Elapsed time
        _timeLabel = new TextBlock
        {
            Text = "0:00", FontSize = 13, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            FontFamily = new FontFamily("Consolas"),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 242, 242, 240)),
            VerticalAlignment = VerticalAlignment.Center
        };
        row.Children.Add(_timeLabel);

        // Stop button
        var stop = new Button
        {
            Content = "■", FontSize = 12, Padding = new Thickness(8, 4, 8, 4),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 224, 62, 62)),
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 255, 255, 255)),
            VerticalAlignment = VerticalAlignment.Center
        };
        stop.Click += (_, _) => _ = App.State.StopRecordingAsync();
        row.Children.Add(stop);

        root.Child = row;
        Content = root;

        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) =>
        {
            var s = (int)(DateTime.Now - _startedAt).TotalSeconds;
            _timeLabel.Text = s >= 3600
                ? $"{s/3600}:{(s%3600)/60:D2}:{s%60:D2}"
                : $"{s/60}:{s%60:D2}";
        };
        _timer.Start();

        // Pulsing animation
        _ = PulseDotAsync(dot);
    }

    public void Show(string title)
    {
        AppWindow.Show();
        Activate();
    }

    private async Task PulseDotAsync(Windows.UI.Xaml.Shapes.Ellipse dot)
    {
        while (AppWindow != null)
        {
            dot.Opacity = dot.Opacity > 0.5 ? 0.3 : 1.0;
            await Task.Delay(700);
        }
    }

    new public void Close()
    {
        _timer?.Stop();
        AppWindow.Destroy();
    }
}
