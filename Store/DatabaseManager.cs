using Microsoft.Data.Sqlite;

namespace TrueMinutes.Windows.Store;

/// Opens and manages the SQLite database file. Windows equivalent of macOS DatabaseManager.swift.
/// DB lives at %APPDATA%\TrueMinutes\trueminutes.db — same data layout as macOS.
public sealed class DatabaseManager : IDisposable
{
    public static readonly DatabaseManager Shared = new();

    private readonly SqliteConnection _connection;

    public SqliteConnection Connection => _connection;

    private DatabaseManager()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TrueMinutes");
        Directory.CreateDirectory(dir);

        var dbPath = Path.Combine(dir, "trueminutes.db");
        var connStr = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
        }.ToString();

        _connection = new SqliteConnection(connStr);
        _connection.Open();

        // Enable WAL mode for better concurrent read performance.
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL; PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();

        Migrations.Apply(_connection);
    }

    public void Dispose() => _connection.Dispose();
}
