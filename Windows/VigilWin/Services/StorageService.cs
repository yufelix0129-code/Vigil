using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using VigilWin.Core;
using VigilWin.Models;

namespace VigilWin.Services;

public sealed class StorageService
{
    private readonly string _databasePath;
    private bool _initialized;

    public StorageService()
    {
        Directory.CreateDirectory(SettingsService.AppDataDirectory);
        _databasePath = Path.Combine(SettingsService.AppDataDirectory, "vigil.db");
    }

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await using var connection = CreateConnection();
        await connection.OpenAsync();

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS FocusSessions (
                Id TEXT PRIMARY KEY,
                Goal TEXT NOT NULL,
                StartTime TEXT NOT NULL,
                EndTime TEXT NULL,
                PlannedDurationSeconds INTEGER NOT NULL,
                FocusedSeconds INTEGER NOT NULL,
                WanderingSeconds INTEGER NOT NULL,
                DistractedSeconds INTEGER NOT NULL,
                IdleSeconds INTEGER NOT NULL,
                DistractionCount INTEGER NOT NULL,
                Summary TEXT NULL
            );
            """);

        await ExecuteNonQueryAsync(connection, """
            CREATE TABLE IF NOT EXISTS FrameRecords (
                Id TEXT PRIMARY KEY,
                SessionId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                Status TEXT NOT NULL,
                Confidence REAL NOT NULL,
                Reason TEXT NOT NULL,
                ScreenshotPath TEXT NULL
            );
            """);

        _initialized = true;
    }

    public async Task CreateSessionAsync(FocusSession session)
    {
        await InitializeAsync();

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FocusSessions (
                Id, Goal, StartTime, EndTime, PlannedDurationSeconds,
                FocusedSeconds, WanderingSeconds, DistractedSeconds, IdleSeconds,
                DistractionCount, Summary
            )
            VALUES (
                $id, $goal, $startTime, $endTime, $plannedDurationSeconds,
                $focusedSeconds, $wanderingSeconds, $distractedSeconds, $idleSeconds,
                $distractionCount, $summary
            );
            """;
        AddSessionParameters(command, session);
        await command.ExecuteNonQueryAsync();
    }

    public async Task UpdateSessionAsync(FocusSession session)
    {
        await InitializeAsync();

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE FocusSessions
            SET Goal = $goal,
                StartTime = $startTime,
                EndTime = $endTime,
                PlannedDurationSeconds = $plannedDurationSeconds,
                FocusedSeconds = $focusedSeconds,
                WanderingSeconds = $wanderingSeconds,
                DistractedSeconds = $distractedSeconds,
                IdleSeconds = $idleSeconds,
                DistractionCount = $distractionCount,
                Summary = $summary
            WHERE Id = $id;
            """;
        AddSessionParameters(command, session);
        await command.ExecuteNonQueryAsync();
    }

    public async Task AddFrameRecordAsync(FrameRecord record)
    {
        await InitializeAsync();

        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO FrameRecords (
                Id, SessionId, Timestamp, Status, Confidence, Reason, ScreenshotPath
            )
            VALUES (
                $id, $sessionId, $timestamp, $status, $confidence, $reason, $screenshotPath
            );
            """;
        command.Parameters.AddWithValue("$id", record.Id.ToString());
        command.Parameters.AddWithValue("$sessionId", record.SessionId.ToString());
        command.Parameters.AddWithValue("$timestamp", record.Timestamp.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$status", record.Status.ToString());
        command.Parameters.AddWithValue("$confidence", record.Confidence);
        command.Parameters.AddWithValue("$reason", record.Reason);
        command.Parameters.AddWithValue("$screenshotPath", (object?)record.ScreenshotPath ?? DBNull.Value);

        await command.ExecuteNonQueryAsync();
    }

    public async Task<List<FocusSession>> GetRecentSessionsAsync(int limit = 20)
    {
        await InitializeAsync();

        var sessions = new List<FocusSession>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Goal, StartTime, EndTime, PlannedDurationSeconds,
                   FocusedSeconds, WanderingSeconds, DistractedSeconds, IdleSeconds,
                   DistractionCount, Summary
            FROM FocusSessions
            ORDER BY StartTime DESC
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            sessions.Add(ReadSession(reader));
        }

        return sessions;
    }

    public async Task<List<FrameRecord>> GetFrameRecordsAsync(Guid sessionId)
    {
        await InitializeAsync();

        var records = new List<FrameRecord>();
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, SessionId, Timestamp, Status, Confidence, Reason, ScreenshotPath
            FROM FrameRecords
            WHERE SessionId = $sessionId
            ORDER BY Timestamp ASC;
            """;
        command.Parameters.AddWithValue("$sessionId", sessionId.ToString());

        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(ReadFrameRecord(reader));
        }

        return records;
    }

    private SqliteConnection CreateConnection()
    {
        return new SqliteConnection($"Data Source={_databasePath}");
    }

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static void AddSessionParameters(SqliteCommand command, FocusSession session)
    {
        command.Parameters.AddWithValue("$id", session.Id.ToString());
        command.Parameters.AddWithValue("$goal", session.Goal);
        command.Parameters.AddWithValue("$startTime", session.StartTime.ToString("O", CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("$endTime", session.EndTime.HasValue
            ? session.EndTime.Value.ToString("O", CultureInfo.InvariantCulture)
            : DBNull.Value);
        command.Parameters.AddWithValue("$plannedDurationSeconds", session.PlannedDurationSeconds);
        command.Parameters.AddWithValue("$focusedSeconds", session.FocusedSeconds);
        command.Parameters.AddWithValue("$wanderingSeconds", session.WanderingSeconds);
        command.Parameters.AddWithValue("$distractedSeconds", session.DistractedSeconds);
        command.Parameters.AddWithValue("$idleSeconds", session.IdleSeconds);
        command.Parameters.AddWithValue("$distractionCount", session.DistractionCount);
        command.Parameters.AddWithValue("$summary", (object?)session.Summary ?? DBNull.Value);
    }

    private static FocusSession ReadSession(SqliteDataReader reader)
    {
        return new FocusSession
        {
            Id = Guid.Parse(reader.GetString(0)),
            Goal = reader.GetString(1),
            StartTime = ParseDateTime(reader.GetString(2)),
            EndTime = reader.IsDBNull(3) ? null : ParseDateTime(reader.GetString(3)),
            PlannedDurationSeconds = reader.GetInt32(4),
            FocusedSeconds = reader.GetInt32(5),
            WanderingSeconds = reader.GetInt32(6),
            DistractedSeconds = reader.GetInt32(7),
            IdleSeconds = reader.GetInt32(8),
            DistractionCount = reader.GetInt32(9),
            Summary = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }

    private static FrameRecord ReadFrameRecord(SqliteDataReader reader)
    {
        var statusText = reader.GetString(3);
        if (!Enum.TryParse<FocusStatus>(statusText, ignoreCase: true, out var status))
        {
            status = FocusStatus.Unknown;
        }

        return new FrameRecord
        {
            Id = Guid.Parse(reader.GetString(0)),
            SessionId = Guid.Parse(reader.GetString(1)),
            Timestamp = ParseDateTime(reader.GetString(2)),
            Status = status,
            Confidence = reader.GetDouble(4),
            Reason = reader.GetString(5),
            ScreenshotPath = reader.IsDBNull(6) ? null : reader.GetString(6)
        };
    }

    private static DateTime ParseDateTime(string value)
    {
        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
