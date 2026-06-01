using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrueMinutes.Windows.Summarize;

namespace TrueMinutes.Windows.App;

public sealed class LiveSegment
{
    public string Id { get; init; } = "";
    public int StartMs { get; init; }
    public string SpeakerLabel { get; init; } = "";
    public string Text { get; init; } = "";
    public bool IsPartial { get; init; }
}

/// Live recording state — Windows equivalent of macOS LiveRecordingStore.swift.
public sealed class LiveRecordingStore : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<LiveSegment> Segments { get; } = [];
    public ObservableCollection<SpeakerParagraph> Paragraphs { get; } = [];

    private string _meetingTitle = "Meeting";
    public string MeetingTitle { get => _meetingTitle; private set => Set(ref _meetingTitle, value); }

    private bool _isActive;
    public bool IsActive { get => _isActive; private set => Set(ref _isActive, value); }

    private DateTime _startedAt = DateTime.Now;
    public int ElapsedSeconds => (int)(DateTime.Now - _startedAt).TotalSeconds;

    public void Begin(string title)
    {
        MeetingTitle = title;
        _startedAt = DateTime.Now;
        Segments.Clear();
        Paragraphs.Clear();
        IsActive = true;
    }

    public void AppendSegment(string id, int startMs, string speakerLabel, string text)
    {
        var seg = new LiveSegment { Id = id, StartMs = startMs, SpeakerLabel = speakerLabel, Text = text };
        // Insert chronologically (parallel decode may deliver out of order)
        int insertAt = Segments.Count;
        for (int i = Segments.Count - 1; i >= 0; i--)
        {
            if (Segments[i].StartMs <= startMs) { insertAt = i + 1; break; }
            if (i == 0) insertAt = 0;
        }
        Segments.Insert(insertAt, seg);
        RefreshHeuristicParagraphs();
    }

    public void SetParagraphs(IEnumerable<SpeakerParagraph> paragraphs)
    {
        Paragraphs.Clear();
        foreach (var p in paragraphs) Paragraphs.Add(p);
    }

    private void RefreshHeuristicParagraphs()
    {
        var turns = Segments.Select(s => new TranscriptFormatter.RawTurn(
            SpeakerKey: s.SpeakerLabel.Contains("You", StringComparison.OrdinalIgnoreCase) ? "you" : "others",
            Text: s.Text,
            StartMs: s.StartMs)).ToList();
        var paras = TranscriptFormatter.HeuristicParagraphs(turns);
        SetParagraphs(paras);
    }

    public void End() { IsActive = false; }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        return true;
    }
}
