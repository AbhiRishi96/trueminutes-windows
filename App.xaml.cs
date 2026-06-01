using Microsoft.UI.Xaml;
using TrueMinutes.Windows.App;
using TrueMinutes.Windows.Store;
using TrueMinutes.Windows.UI.TrayIcon;

namespace TrueMinutes.Windows;

public partial class App : Application
{
    public static AppState State { get; } = new();
    private MainWindow? _mainWindow;
    private TrayIconManager? _trayIcon;

    public App()
    {
        InitializeComponent();
        RequestedTheme = ApplicationTheme.Dark; // dark-first, matches macOS
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        // Initialise database on startup.
        _ = DatabaseManager.Shared;

        // Main window — hidden until user clicks tray or a meeting is detected.
        _mainWindow = new MainWindow();
        _mainWindow.Activate();

        // System tray icon.
        _trayIcon = new TrayIconManager(_mainWindow, State);
        _trayIcon.Install();

        // Start meeting detector.
        State.StartDetector();
    }

    /// Show (and bring to front) the main window from anywhere.
    public static void ShowMainWindow()
    {
        var app = (App)Current;
        app._mainWindow?.BringToFront();
    }
}
