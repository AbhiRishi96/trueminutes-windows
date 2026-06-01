using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace TrueMinutes.Windows.Detect;

/// Polls running processes + frontmost window to detect meeting apps.
/// Windows equivalent of NSWorkspace.runningApplications + NSWorkspace.frontmostApplication.
public static class ProcessMonitor
{
    // --- Process name → platform mapping (equivalent to macOS AppWatchlist bundle IDs) ---
    private static readonly Dictionary<string, MeetingPlatform> ProcessMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Zoom"]                  = MeetingPlatform.Zoom,
        ["Zoom.exe"]              = MeetingPlatform.Zoom,
        ["ms-teams"]              = MeetingPlatform.Teams,
        ["ms-teams.exe"]          = MeetingPlatform.Teams,
        ["Teams"]                 = MeetingPlatform.Teams,
        ["Teams.exe"]             = MeetingPlatform.Teams,
        ["msedgewebview2"]        = MeetingPlatform.Teams,   // Teams 2.x uses Edge WebView2
        ["CiscoWebExStart"]       = MeetingPlatform.Webex,
        ["webex"]                 = MeetingPlatform.Webex,
        ["Webex.exe"]             = MeetingPlatform.Webex,
        ["slack"]                 = MeetingPlatform.Slack,
        ["slack.exe"]             = MeetingPlatform.Slack,
        ["Discord"]               = MeetingPlatform.Discord,
        ["Discord.exe"]           = MeetingPlatform.Discord,
    };

    /// Return all running meeting platforms (some may run multiple processes).
    public static HashSet<MeetingPlatform> RunningMeetingPlatforms()
    {
        var result = new HashSet<MeetingPlatform>();
        try
        {
            foreach (var proc in Process.GetProcesses())
            {
                try
                {
                    if (ProcessMap.TryGetValue(proc.ProcessName, out var platform))
                        result.Add(platform);
                }
                catch { /* process may have exited */ }
            }
        }
        catch { }
        return result;
    }

    /// Returns (processId, platformName) of the foreground window's process, or null.
    public static (int Pid, string ProcessName)? ForegroundProcess()
    {
        var hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return null;
        GetWindowThreadProcessId(hwnd, out uint pid);
        try
        {
            var proc = Process.GetProcessById((int)pid);
            return ((int)pid, proc.ProcessName);
        }
        catch { return null; }
    }

    /// Enumerate all visible window titles — equivalent to CGWindowListCopyWindowInfo.
    public static List<(int Pid, string Title)> AllVisibleWindowTitles()
    {
        var result = new List<(int, string)>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd)) return true;
            var len = GetWindowTextLength(hwnd);
            if (len == 0) return true;
            var sb = new StringBuilder(len + 1);
            GetWindowText(hwnd, sb, sb.Capacity);
            GetWindowThreadProcessId(hwnd, out uint pid);
            result.Add(((int)pid, sb.ToString()));
            return true; // continue enumeration
        }, IntPtr.Zero);
        return result;
    }

    // --- Win32 P/Invoke ---
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
    [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int GetWindowTextLength(IntPtr hWnd);
    [DllImport("user32.dll")] static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
}
