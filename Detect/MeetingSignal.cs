namespace TrueMinutes.Windows.Detect;

/// A detected meeting — equivalent to macOS MeetingSignal struct.
public sealed record MeetingSignal(
    string AppName,
    MeetingPlatform Platform,
    int ProcessId,
    bool MicInUse,
    string? MeetingTitle = null,
    string? JoinUrl = null);

/// Events emitted by MeetingDetector.
public abstract record MeetingDetectorEvent;
public sealed record MeetingStartedEvent(MeetingSignal Signal) : MeetingDetectorEvent;
public sealed record MeetingEndedEvent : MeetingDetectorEvent;
