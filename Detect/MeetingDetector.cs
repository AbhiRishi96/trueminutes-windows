using System.Threading.Channels;

namespace TrueMinutes.Windows.Detect;

/// Polls running processes, window titles, mic state, and browser tabs to detect
/// meeting start/end events. Windows equivalent of macOS MeetingDetector.swift.
///
/// The detector emits MeetingDetectorEvent into a Channel so the AppState can consume
/// them on a dedicated task without blocking the detection loop.
public sealed class MeetingDetector : IAsyncDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _pollTask;
    private MeetingPlatform? _currentPlatform;
    private readonly Channel<MeetingDetectorEvent> _events;
    private const int PollMs = 500;

    public ChannelReader<MeetingDetectorEvent> Events => _events.Reader;

    public MeetingDetector()
    {
        _events = Channel.CreateBounded<MeetingDetectorEvent>(new BoundedChannelOptions(32)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
    }

    public void Start()
    {
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _pollTask = Task.Run(async () =>
        {
            while (!ct.IsCancellationRequested)
            {
                await PollAsync(ct);
                await Task.Delay(PollMs, ct);
            }
        }, ct);
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_pollTask is not null)
        {
            try { await _pollTask; } catch (OperationCanceledException) { }
        }
        _events.Writer.TryComplete();
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var signal = await DetectAsync(ct);

        if (signal is not null && _currentPlatform != signal.Platform)
        {
            _currentPlatform = signal.Platform;
            _events.Writer.TryWrite(new MeetingStartedEvent(signal));
        }
        else if (signal is null && _currentPlatform is not null)
        {
            _currentPlatform = null;
            _events.Writer.TryWrite(new MeetingEndedEvent());
        }
    }

    private async Task<MeetingSignal?> DetectAsync(CancellationToken ct)
    {
        // 1. Check running native meeting apps.
        var running = ProcessMonitor.RunningMeetingPlatforms();
        var foreground = ProcessMonitor.ForegroundProcess();
        var windowTitles = ProcessMonitor.AllVisibleWindowTitles();

        foreach (var platform in running)
        {
            // Check window titles for active-call indicators.
            foreach (var (pid, title) in windowTitles)
            {
                if (!string.IsNullOrWhiteSpace(title) &&
                    MeetingWindowPatterns.LooksLikeActiveCall(title, platform))
                {
                    var micState = GetMicState(platform, pid);
                    var meetingTitle = platform == MeetingPlatform.Slack
                        ? MeetingWindowPatterns.SlackHuddleTitle(title)
                        : null;
                    return new MeetingSignal(
                        platform.DisplayName(), platform, pid,
                        MicInUse: micState == MicActivityMonitor.MicState.Hot,
                        MeetingTitle: meetingTitle);
                }
            }

            // Mic-only fallback for apps without reliable title patterns.
            if (platform is MeetingPlatform.Zoom or MeetingPlatform.Teams or MeetingPlatform.Webex)
            {
                var micState = GetMicState(platform, foreground?.Pid ?? 0);
                if (micState == MicActivityMonitor.MicState.Hot)
                {
                    return new MeetingSignal(platform.DisplayName(), platform,
                        foreground?.Pid ?? 0, MicInUse: true);
                }
            }
        }

        // 2. Check browser tabs via CDP (Google Meet, Teams web, Zoom web).
        try
        {
            var meetingTabs = await BrowserCdpInspector.ActiveMeetingTabsAsync();
            if (meetingTabs.Count > 0)
            {
                var tab = meetingTabs[0];
                var platform = MeetingWindowPatterns.PlatformFromUrl(tab.Url) ?? MeetingPlatform.GoogleMeet;
                return new MeetingSignal(
                    platform.DisplayName(), platform, tab.BrowserPid,
                    MicInUse: MicActivityMonitor.IsMicrophoneInUseByAnyApp(),
                    JoinUrl: tab.Url);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch { /* CDP unavailable — skip */ }

        return null;
    }

    private static MicActivityMonitor.MicState GetMicState(MeetingPlatform platform, int primaryPid)
    {
        return platform == MeetingPlatform.Teams
            ? MicActivityMonitor.TeamssMicState()
            : MicActivityMonitor.MicStateForPids([primaryPid]);
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}
