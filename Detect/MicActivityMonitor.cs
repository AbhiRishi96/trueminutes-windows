using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace TrueMinutes.Windows.Detect;

/// Determines whether a meeting app (by PID) is currently consuming the microphone.
/// Windows equivalent of macOS kAudioProcessPropertyIsRunningInput + proc_pidpath bundle matching.
///
/// Windows approach: enumerate IAudioSessionManager2 audio sessions on the default input device,
/// find sessions matching the meeting app PID, and check their state (Active = mic in use).
/// This is the Windows equivalent of CoreAudio's per-process 'pinp' property.
public static class MicActivityMonitor
{
    private static readonly MMDeviceEnumerator Enumerator = new();

    public enum MicState { Hot, Cold, Unknown }

    /// Is the default microphone device currently in use by ANY app (system-wide)?
    /// Used in the detection phase (before recording starts). Equivalent to
    /// macOS kAudioDevicePropertyDeviceIsRunningSomewhere.
    public static bool IsMicrophoneInUseByAnyApp()
    {
        try
        {
            var device = Enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            var sessions = device.AudioSessionManager.Sessions;
            for (int i = 0; i < sessions.Count; i++)
            {
                if (sessions[i].State == AudioSessionState.AudioSessionStateActive)
                    return true;
            }
        }
        catch { }
        return false;
    }

    /// Is the microphone hot specifically for the given process(es)?
    /// Equivalent to macOS isMicHot(bundleIdentifier:) with Electron helper detection.
    public static MicState MicStateForPids(IEnumerable<int> pids)
    {
        var pidSet = new HashSet<int>(pids);
        if (pidSet.Count == 0) return MicState.Unknown;

        try
        {
            var device = Enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            var sessions = device.AudioSessionManager.Sessions;
            bool anyRunning = false;

            for (int i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                var pid = (int)session.GetProcessID;
                if (!pidSet.Contains(pid)) continue;

                anyRunning = true;
                if (session.State == AudioSessionState.AudioSessionStateActive)
                    return MicState.Hot;
            }
            return anyRunning ? MicState.Cold : MicState.Unknown;
        }
        catch { return MicState.Unknown; }
    }

    /// Get all PIDs for processes with the given name (e.g. "Zoom", "ms-teams").
    /// Handles multi-process apps like Teams 2.x (main + msedgewebview2 helper).
    public static IReadOnlyList<int> PidsForProcessName(string processName)
    {
        var result = new List<int>();
        try
        {
            foreach (var proc in System.Diagnostics.Process.GetProcessesByName(processName))
                result.Add(proc.Id);
        }
        catch { }
        return result;
    }

    /// Returns true if ANY of the Teams-related processes (ms-teams, msedgewebview2, Teams) hold the mic.
    /// Teams 2.x runs audio in a dedicated msedgewebview2.exe child process, not in ms-teams.exe itself.
    public static MicState TeamssMicState()
    {
        var pids = new List<int>();
        foreach (var name in new[] { "ms-teams", "Teams", "msedgewebview2" })
            pids.AddRange(PidsForProcessName(name));
        return MicStateForPids(pids);
    }
}
