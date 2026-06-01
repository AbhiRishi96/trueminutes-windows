namespace TrueMinutes.Windows.Store;

public sealed class MeetingRecord
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public long StartedAt { get; set; }
    public long? EndedAt { get; set; }
    public string Status { get; set; } = "";
    public string? SummaryStatus { get; set; }
    public string? Recipe { get; set; }
    public string? AutoCategory { get; set; }
    public string? FormattedTranscriptJson { get; set; }

    public DateTime StartDate => DateTimeOffset.FromUnixTimeSeconds(StartedAt).LocalDateTime;

    public string FormattedDate => StartDate.ToString("ddd, MMM d · h:mm tt");

    public string CategoryDisplay => AutoCategory switch
    {
        "one_on_one" => "1:1",
        "team"       => "Team",
        "customer"   => "Customer",
        "standup"    => "Standup",
        "review"     => "Review",
        _            => ""
    };
}

public sealed class MeetingRepository
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _db = DatabaseManager.Shared.Connection;

    public List<MeetingRecord> AllMeetings(bool archivedOnly = false)
    {
        const string sql = @"
            SELECT id, title, started_at, ended_at, status, summary_status, recipe, auto_category
            FROM meeting
            WHERE archived_at IS NULL
            ORDER BY started_at DESC
            LIMIT 200";
        using var cmd = _db.CreateCommand();
        cmd.CommandText = sql;
        var result = new List<MeetingRecord>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            result.Add(new MeetingRecord
            {
                Id          = reader.GetString(0),
                Title       = reader.GetString(1),
                StartedAt   = reader.GetInt64(2),
                EndedAt     = reader.IsDBNull(3) ? null : reader.GetInt64(3),
                Status      = reader.GetString(4),
                SummaryStatus = reader.IsDBNull(5) ? null : reader.GetString(5),
                Recipe       = reader.IsDBNull(6) ? null : reader.GetString(6),
                AutoCategory = reader.IsDBNull(7) ? null : reader.GetString(7),
            });
        }
        return result;
    }

    public MeetingRecord? GetMeeting(string id)
    {
        using var cmd = _db.CreateCommand();
        cmd.CommandText = "SELECT id,title,started_at,ended_at,status,summary_status,recipe,auto_category,formatted_transcript_json FROM meeting WHERE id=@id";
        cmd.Parameters.AddWithValue("@id", id);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;
        return new MeetingRecord
        {
            Id                    = reader.GetString(0),
            Title                 = reader.GetString(1),
            StartedAt             = reader.GetInt64(2),
            EndedAt               = reader.IsDBNull(3) ? null : reader.GetInt64(3),
            Status                = reader.GetString(4),
            SummaryStatus         = reader.IsDBNull(5) ? null : reader.GetString(5),
            Recipe                = reader.IsDBNull(6) ? null : reader.GetString(6),
            AutoCategory          = reader.IsDBNull(7) ? null : reader.GetString(7),
            FormattedTranscriptJson = reader.IsDBNull(8) ? null : reader.GetString(8),
        };
    }
}
