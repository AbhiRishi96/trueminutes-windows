using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TrueMinutes.Windows.Detect;
using TrueMinutes.Windows.Store;

namespace TrueMinutes.Windows.App;

/// Central application state — Windows equivalent of macOS AppState.swift (@MainActor ObservableObject).
/// Uses INotifyPropertyChanged + ObservableCollection for WinUI 3 data binding.
public sealed class AppState : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // ─── Status ────────────────────────────────────────────────────────────
    public enum AppStatus { Idle, Detecting, Recording, Stopping, Summarizing, Error }

    private AppStatus _status = AppStatus.Idle;
    public AppStatus Status { get => _status; private set => Set(ref _status, value); }

    public bool IsRecording => Status == AppStatus.Recording;
    public bool IsIdle      => Status == AppStatus.Idle;

    private string _statusLabel = "Idle";
    public string StatusLabel { get => _statusLabel; private set => Set(ref _statusLabel, value); }

    private string? _lastError;
    public string? LastError { get => _lastError; private set => Set(ref _lastError, value); }

    // ─── Navigation ─────────────────────────────────────────────────────────
    public enum ShellSection { Meetings, LiveTranscript, Ask, Settings }

    private ShellSection _shellSection = ShellSection.Meetings;
    public ShellSection Section { get => _shellSection; set => Set(ref _shellSection, value); }

    private string? _selectedMeetingId;
    public string? SelectedMeetingId { get => _selectedMeetingId; set => Set(ref _selectedMeetingId, value); }

    // ─── Live recording ──────────────────────────────────────────────────────
    public LiveRecordingStore LiveStore { get; } = new();

    // ─── Meetings library ────────────────────────────────────────────────────
    public ObservableCollection<MeetingRecord> Meetings { get; } = [];

    // ─── Detected meeting prompt ─────────────────────────────────────────────
    private MeetingSignal? _lastDetectedMeeting;
    public MeetingSignal? LastDetectedMeeting { get => _lastDetectedMeeting; private set => Set(ref _lastDetectedMeeting, value); }

    // ─── Recording notice (system audio warnings, etc.) ──────────────────────
    private string? _recordingNotice;
    public string? RecordingNotice { get => _recordingNotice; private set => Set(ref _recordingNotice, value); }

    // ─── Auto-stop countdown ────────────────────────────────────────────────
    private int? _autoStopSecondsLeft;
    public int? AutoStopSecondsLeft { get => _autoStopSecondsLeft; private set => Set(ref _autoStopSecondsLeft, value); }

    // ─── Detection ──────────────────────────────────────────────────────────
    private readonly MeetingDetector _detector = new();
    private Task? _detectorTask;

    // DispatcherQueue captured on construction (must be called from the UI thread).
    private Microsoft.UI.Dispatching.DispatcherQueue? _uiDispatcher;

    /// Must be called once from the UI thread so we capture its DispatcherQueue.
    public void StartDetector()
    {
        _uiDispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _detector.Start();
        _detectorTask = Task.Run(ConsumeMeetingEventsAsync);
        RefreshMeetings();
    }

    private async Task ConsumeMeetingEventsAsync()
    {
        await foreach (var evt in _detector.Events.ReadAllAsync())
        {
            var captured = evt;
            _uiDispatcher?.TryEnqueue(async () =>
            {
                switch (captured)
                {
                    case MeetingStartedEvent { Signal: var signal }:
                        await HandleMeetingStartedAsync(signal);
                        break;
                    case MeetingEndedEvent:
                        HandleMeetingEnded();
                        break;
                }
            });
            await Task.Delay(10); // yield between events
        }
    }

    private async Task HandleMeetingStartedAsync(MeetingSignal signal)
    {
        if (Status is AppStatus.Recording or AppStatus.Summarizing) return;
        LastDetectedMeeting = signal;
        Status = AppStatus.Detecting;
        StatusLabel = $"Meeting detected in {signal.Platform.DisplayName()}";
        // Show prompt — wired from MainWindow via event
        MeetingDetected?.Invoke(signal);
        await Task.CompletedTask;
    }

    private void HandleMeetingEnded()
    {
        if (Status == AppStatus.Detecting)
        {
            Status = AppStatus.Idle;
            StatusLabel = "Idle";
            LastDetectedMeeting = null;
        }
        else if (Status == AppStatus.Recording)
        {
            _ = StopRecordingAsync();
        }
    }

    /// Raised when a meeting is detected — UI subscribes to show the prompt.
    public event Action<MeetingSignal>? MeetingDetected;

    // ─── Recording actions ──────────────────────────────────────────────────
    public void UserAcceptedMeeting(MeetingSignal signal)
    {
        _ = StartRecordingAsync(signal);
    }

    public void UserDeclinedMeeting()
    {
        Status = AppStatus.Idle;
        StatusLabel = "Idle";
        LastDetectedMeeting = null;
    }

    private async Task StartRecordingAsync(MeetingSignal signal)
    {
        Status = AppStatus.Recording;
        StatusLabel = $"Recording — {signal.MeetingTitle ?? signal.Platform.DisplayName()}";
        LiveStore.Begin(signal.MeetingTitle ?? signal.Platform.DisplayName());
        Section = ShellSection.LiveTranscript;
        RecordingStarted?.Invoke(signal);
        await Task.CompletedTask;
    }

    public async Task StopRecordingAsync()
    {
        if (Status != AppStatus.Recording) return;
        Status = AppStatus.Stopping;
        StatusLabel = "Saving last words…";
        LiveStore.End();
        RecordingStopped?.Invoke();

        // TODO: run summarization pipeline
        await Task.Delay(1000);

        Status = AppStatus.Idle;
        StatusLabel = "Idle";
        Section = ShellSection.Meetings;
        RefreshMeetings();
    }

    public event Action<MeetingSignal>? RecordingStarted;
    public event Action? RecordingStopped;

    // ─── Library ─────────────────────────────────────────────────────────────
    public void RefreshMeetings()
    {
        try
        {
            var repo = new MeetingRepository();
            var meetings = repo.AllMeetings();
            Meetings.Clear();
            foreach (var m in meetings) Meetings.Add(m);
        }
        catch { }
    }

    // ─── INotifyPropertyChanged ───────────────────────────────────────────────
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        if (name is nameof(Status)) PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRecording)));
        return true;
    }
}
