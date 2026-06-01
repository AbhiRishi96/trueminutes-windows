using System.Drawing;
using System.Windows.Forms;
using TrueMinutes.Windows.App;

namespace TrueMinutes.Windows.UI.TrayIcon;

/// System tray icon — Windows equivalent of macOS NSStatusItem.
/// Uses WinForms NotifyIcon (the standard Windows API for system-tray icons).
/// Requires adding `<UseWindowsForms>true</UseWindowsForms>` to the .csproj.
public sealed class TrayIconManager : IDisposable
{
    private readonly Microsoft.UI.Xaml.Window _mainWindow;
    private readonly AppState _state;
    private NotifyIcon? _notifyIcon;
    private ContextMenuStrip? _contextMenu;

    public TrayIconManager(Microsoft.UI.Xaml.Window mainWindow, AppState state)
    {
        _mainWindow = mainWindow;
        _state      = state;
        _state.PropertyChanged += OnStateChanged;
    }

    public void Install()
    {
        // Build context menu.
        _contextMenu = new ContextMenuStrip();
        _contextMenu.Items.Add("Open TrueMinutes",  null, (_, _) => App.ShowMainWindow());
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Stop Recording", null, (_, _) => _ = _state.StopRecordingAsync())
                          .Enabled = false;
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add("Quit", null, (_, _) =>
        {
            _notifyIcon?.Dispose();
            System.Windows.Forms.Application.Exit();
            Microsoft.UI.Xaml.Application.Current.Exit();
        });

        _notifyIcon = new NotifyIcon
        {
            Text            = "TrueMinutes",
            Icon            = LoadIcon(),
            Visible         = true,
            ContextMenuStrip = _contextMenu
        };
        _notifyIcon.DoubleClick += (_, _) => App.ShowMainWindow();
    }

    private void OnStateChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(AppState.Status)) return;
        if (_contextMenu?.Items[2] is ToolStripMenuItem stopItem)
            stopItem.Enabled = _state.IsRecording;

        if (_notifyIcon != null)
            _notifyIcon.Text = _state.IsRecording
                ? $"TrueMinutes — Recording ({_state.LiveStore.MeetingTitle})"
                : "TrueMinutes";
    }

    private static Icon LoadIcon()
    {
        // Try to load AppIcon.ico from Resources; fall back to a generated icon.
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Resources", "AppIcon.ico");
        return File.Exists(iconPath)
            ? new Icon(iconPath)
            : DrawFallbackIcon();
    }

    private static Icon DrawFallbackIcon()
    {
        // Generate a simple indigo square icon as fallback when no .ico file is present.
        using var bmp = new Bitmap(32, 32);
        using var g   = Graphics.FromImage(bmp);
        using var brush = new SolidBrush(ColorTranslator.FromHtml("#6E56F7"));
        g.FillRectangle(brush, 0, 0, 32, 32);
        using var font = new System.Drawing.Font("Segoe UI", 12f, FontStyle.Bold);
        using var text = new SolidBrush(Color.White);
        g.DrawString("T", font, text, 8, 6);
        return Icon.FromHandle(bmp.GetHicon());
    }

    public void Dispose()
    {
        _notifyIcon?.Dispose();
        _contextMenu?.Dispose();
    }
}
