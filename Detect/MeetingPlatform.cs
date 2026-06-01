namespace TrueMinutes.Windows.Detect;

public enum MeetingPlatform
{
    Zoom,
    Teams,
    GoogleMeet,
    Webex,
    Slack,
    Discord,
    Unknown
}

public static class MeetingPlatformInfo
{
    public static string DisplayName(this MeetingPlatform p) => p switch
    {
        MeetingPlatform.Zoom       => "Zoom",
        MeetingPlatform.Teams      => "Microsoft Teams",
        MeetingPlatform.GoogleMeet => "Google Meet",
        MeetingPlatform.Webex      => "Webex",
        MeetingPlatform.Slack      => "Slack",
        MeetingPlatform.Discord    => "Discord",
        _                          => "Meeting"
    };
}
