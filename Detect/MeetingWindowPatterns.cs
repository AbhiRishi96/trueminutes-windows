using System.Text.RegularExpressions;

namespace TrueMinutes.Windows.Detect;

/// Window title pattern matching — direct port of macOS MeetingWindowPatterns.swift.
/// Used by the meeting detector to distinguish an active call from a background app window.
public static class MeetingWindowPatterns
{
    /// Returns true if the window title indicates an active call for the given platform.
    public static bool LooksLikeActiveCall(string title, MeetingPlatform platform)
    {
        if (string.IsNullOrWhiteSpace(title)) return false;
        var t = title.ToLowerInvariant();
        return platform switch
        {
            MeetingPlatform.Zoom => t.Contains("zoom meeting") || t.Contains("zoom - ")
                                 || t.Contains("video conference") || t.Contains("meeting")
                                 || t.Contains("webinar"),
            MeetingPlatform.Teams => t.Contains("in a call") || t.Contains("teams meeting")
                                  || t.Contains("meeting with") || t.Contains("| call")
                                  || (t.Contains("microsoft teams") &&
                                      (t.Contains("meeting") || t.Contains("call")
                                    || t.Contains("presenting") || t.Contains("sharing"))),
            MeetingPlatform.Webex => t.Contains("cisco webex") || t.Contains("webex meeting")
                                  || t.Contains("in a call"),
            MeetingPlatform.Slack => LooksLikeSlackCall(t),
            MeetingPlatform.Discord => t.Contains("voice connected") || t.Contains("in a call")
                                    || t.Contains("screen share"),
            MeetingPlatform.GoogleMeet => t.Contains("meet.google.com"),
            _ => false
        };
    }

    public static bool LooksLikeSlackCall(string lowerTitle)
    {
        return lowerTitle.Contains("huddle in #")
            || lowerTitle.Contains("huddle with ")
            || lowerTitle.Contains("in a huddle")
            || lowerTitle.Contains("started a huddle")
            || lowerTitle.Contains("slack call")
            || lowerTitle.Contains("listening in to ")
            || (lowerTitle.Contains("call with ") && lowerTitle.Contains("slack"));
    }

    /// Parse a Slack huddle title into a clean meeting name.
    public static string? SlackHuddleTitle(string? rawTitle)
    {
        if (rawTitle is null) return null;
        var title = rawTitle;

        string? After(string marker)
        {
            var idx = title.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            var tail = title[(idx + marker.Length)..];
            foreach (var sep in new[] { " — ", " – ", " - ", " | " })
            {
                var si = tail.IndexOf(sep, StringComparison.Ordinal);
                if (si >= 0) tail = tail[..si];
            }
            return tail.Replace("@", "").Trim().NullIfEmpty();
        }

        if (After("Listening in to ") is { } ch1) return $"Huddle: {ch1}";
        if (After("Huddle in ") is { } ch2)       return $"Huddle: {ch2}";
        if (After("Huddle with ") is { } who)      return $"Huddle with {who}";
        if (After("Slack Call with ") is { } w2)   return $"Call with {w2}";
        if (After("Call with ") is { } w3)          return $"Call with {w3}";
        if (rawTitle.Contains("huddle", StringComparison.OrdinalIgnoreCase)) return "Slack huddle";
        return null;
    }

    // Browser URL patterns for Google Meet / Teams web / Zoom web
    public static MeetingPlatform? PlatformFromUrl(string url)
    {
        if (url.Contains("meet.google.com"))    return MeetingPlatform.GoogleMeet;
        if (url.Contains("teams.microsoft.com") || url.Contains("teams.live.com"))
                                                return MeetingPlatform.Teams;
        if (url.Contains("zoom.us/wc") || url.Contains("zoom.us/j/"))
                                                return MeetingPlatform.Zoom;
        if (url.Contains("app.webex.com"))      return MeetingPlatform.Webex;
        return null;
    }
}

file static class StringExt
{
    public static string? NullIfEmpty(this string s) => string.IsNullOrWhiteSpace(s) ? null : s;
}
