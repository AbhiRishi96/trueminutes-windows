using Microsoft.Data.Sqlite;

namespace TrueMinutes.Windows.Store;

/// SQLite schema migrations — same v1–v13 schema as macOS GRDB migrations.
/// Uses IF NOT EXISTS / simple version-table guards so it's safe to run on every launch.
public static class Migrations
{
    private const string VersionTable = "schema_version";

    public static void Apply(SqliteConnection db)
    {
        CreateVersionTable(db);
        int current = GetVersion(db);

        if (current < 1)  { ApplyV1(db);  SetVersion(db, 1); }
        if (current < 2)  { ApplyV2(db);  SetVersion(db, 2); }
        if (current < 3)  { ApplyV3(db);  SetVersion(db, 3); }
        if (current < 11) { ApplyV11(db); SetVersion(db, 11); }
        if (current < 12) { ApplyV12(db); SetVersion(db, 12); }
        if (current < 13) { ApplyV13(db); SetVersion(db, 13); }
    }

    // v1: core tables
    private static void ApplyV1(SqliteConnection db) => db.Execute(@"
        CREATE TABLE IF NOT EXISTS meeting (
            id TEXT PRIMARY KEY,
            title TEXT NOT NULL DEFAULT '',
            started_at INTEGER NOT NULL,
            ended_at INTEGER,
            detected_app_bundle_id TEXT,
            language_setting TEXT NOT NULL DEFAULT 'translateToEnglish',
            transcribe_engine TEXT NOT NULL DEFAULT 'whisper',
            status TEXT NOT NULL DEFAULT 'recording',
            folder_id TEXT,
            summary_status TEXT,
            summary_error TEXT,
            notes_markdown TEXT,
            archived_at INTEGER,
            created_at INTEGER NOT NULL,
            updated_at INTEGER NOT NULL
        );
        CREATE TABLE IF NOT EXISTS transcript_segment (
            id TEXT PRIMARY KEY,
            meeting_id TEXT NOT NULL REFERENCES meeting(id) ON DELETE CASCADE,
            seq INTEGER NOT NULL,
            start_ms INTEGER NOT NULL DEFAULT 0,
            end_ms INTEGER NOT NULL DEFAULT 0,
            source TEXT NOT NULL DEFAULT 'system',
            speaker_label TEXT NOT NULL DEFAULT '',
            text_original TEXT NOT NULL DEFAULT ''
        );
        CREATE INDEX IF NOT EXISTS idx_segment_meeting ON transcript_segment(meeting_id, start_ms);
        CREATE VIRTUAL TABLE IF NOT EXISTS transcript_segment_fts USING fts5(
            text_original, content=transcript_segment, content_rowid=rowid
        );
        CREATE TABLE IF NOT EXISTS meeting_summary (
            id TEXT PRIMARY KEY,
            meeting_id TEXT NOT NULL REFERENCES meeting(id) ON DELETE CASCADE,
            summary_markdown TEXT NOT NULL DEFAULT '',
            summary_bullets TEXT NOT NULL DEFAULT '[]',
            decisions_json TEXT NOT NULL DEFAULT '[]',
            action_items_json TEXT NOT NULL DEFAULT '[]',
            open_questions_json TEXT NOT NULL DEFAULT '[]',
            model_used TEXT
        );
        CREATE TABLE IF NOT EXISTS folder (
            id TEXT PRIMARY KEY,
            name TEXT NOT NULL DEFAULT '',
            created_at INTEGER NOT NULL
        );
    ");

    // v2: speaker label column (already in v1 above; GRDB needed ALTER, we use IF NOT EXISTS)
    private static void ApplyV2(SqliteConnection db) { /* handled in v1 above */ }

    // v3: summary table
    private static void ApplyV3(SqliteConnection db) { /* handled in v1 above */ }

    // v11: meeting classification
    private static void ApplyV11(SqliteConnection db) => db.Execute(@"
        ALTER TABLE meeting ADD COLUMN recipe TEXT;
        ALTER TABLE meeting ADD COLUMN auto_category TEXT;
        ALTER TABLE meeting ADD COLUMN attendee_count INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE meeting ADD COLUMN attendee_domains_json TEXT NOT NULL DEFAULT '[]';
        ALTER TABLE meeting ADD COLUMN classified_at INTEGER;
        ALTER TABLE folder ADD COLUMN smart_rule_json TEXT;
    ");

    // v12: raw audio metadata
    private static void ApplyV12(SqliteConnection db) => db.Execute(@"
        ALTER TABLE meeting ADD COLUMN has_raw_audio INTEGER NOT NULL DEFAULT 0;
        ALTER TABLE meeting ADD COLUMN raw_audio_recorded_at INTEGER;
    ");

    // v13: LLM-polished paragraph transcript
    private static void ApplyV13(SqliteConnection db) => db.Execute(@"
        ALTER TABLE meeting ADD COLUMN formatted_transcript_json TEXT;
    ");

    private static void CreateVersionTable(SqliteConnection db) => db.Execute($@"
        CREATE TABLE IF NOT EXISTS {VersionTable} (version INTEGER NOT NULL DEFAULT 0);
        INSERT OR IGNORE INTO {VersionTable} (version) VALUES (0);
    ");

    private static int GetVersion(SqliteConnection db)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"SELECT version FROM {VersionTable} LIMIT 1";
        return Convert.ToInt32(cmd.ExecuteScalar() ?? 0);
    }

    private static void SetVersion(SqliteConnection db, int version)
    {
        using var cmd = db.CreateCommand();
        cmd.CommandText = $"UPDATE {VersionTable} SET version = @v";
        cmd.Parameters.AddWithValue("@v", version);
        cmd.ExecuteNonQuery();
    }
}

// Convenience extension
file static class SqliteExt
{
    public static void Execute(this SqliteConnection db, string sql)
    {
        // Run each statement separately (SQLite doesn't support multi-statement Execute)
        foreach (var stmt in sql.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (string.IsNullOrWhiteSpace(stmt)) continue;
            try
            {
                using var cmd = db.CreateCommand();
                cmd.CommandText = stmt;
                cmd.ExecuteNonQuery();
            }
            catch (SqliteException ex) when (ex.Message.Contains("duplicate column"))
            {
                // Idempotent ALTER TABLE — column already exists, safe to ignore.
            }
        }
    }
}
