namespace TrueMinutes.Windows.Summarize;

/// One readable paragraph attributed to a speaker turn.
/// Direct port of macOS SpeakerParagraph.swift.
public sealed record SpeakerParagraph(string Speaker, string Text, int? StartMs = null)
{
    /// Human-facing label for the subtle turn cue.
    public string DisplaySpeaker => Speaker.ToLowerInvariant() switch
    {
        "you" or "me" or "self" => "You",
        "others" or "other" or "remote" or "participant" or "participants" => "Others",
        _ => string.IsNullOrEmpty(Speaker) ? "Others" : Speaker
    };

    /// Normalized bucket used to decide whether the turn cue should change.
    public string TurnKey => Speaker.ToLowerInvariant() switch
    {
        "you" or "me" or "self" => "you",
        _ => DisplaySpeaker
    };
}
