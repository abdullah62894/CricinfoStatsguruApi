using CricinfoStatsguruApi.Models;
using CricinfoStatsguruApi.Options;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CricinfoStatsguruApi.Services;

public sealed class SqliteStatsCacheRepository : IStatsCacheRepository
{
    private readonly string _connectionString;

    public SqliteStatsCacheRepository(IOptions<CacheOptions> options, IHostEnvironment environment)
    {
        var databasePath = options.Value.DatabasePath;
        var fullPath = Path.GetFullPath(databasePath, environment.ContentRootPath);
        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = fullPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();

        EnsureSchema();
    }

    public async Task<CachedUrlEntry?> TryGetUrlAsync(string url, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT url, response_html, fetched_utc, expires_utc
            FROM url_cache
            WHERE url = $url AND expires_utc > $now
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$url", url);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CachedUrlEntry
        {
            Url = reader.GetString(0),
            Html = reader.GetString(1),
            FetchedUtc = DateTimeOffset.Parse(reader.GetString(2)),
            ExpiresUtc = DateTimeOffset.Parse(reader.GetString(3))
        };
    }

    public async Task SaveUrlAsync(string url, string html, DateTimeOffset fetchedUtc, DateTimeOffset expiresUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO url_cache (url, response_html, fetched_utc, expires_utc)
            VALUES ($url, $html, $fetchedUtc, $expiresUtc)
            ON CONFLICT(url) DO UPDATE SET
                response_html = excluded.response_html,
                fetched_utc = excluded.fetched_utc,
                expires_utc = excluded.expires_utc;
            """;
        command.Parameters.AddWithValue("$url", url);
        command.Parameters.AddWithValue("$html", html);
        command.Parameters.AddWithValue("$fetchedUtc", fetchedUtc.ToString("O"));
        command.Parameters.AddWithValue("$expiresUtc", expiresUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<CachedProfileEntry?> TryGetProfileAsync(string cacheKey, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT cache_key, profile_json, fetched_utc, expires_utc
            FROM profile_cache
            WHERE cache_key = $cacheKey AND expires_utc > $now
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$cacheKey", cacheKey);
        command.Parameters.AddWithValue("$now", DateTimeOffset.UtcNow.ToString("O"));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new CachedProfileEntry
        {
            CacheKey = reader.GetString(0),
            PayloadJson = reader.GetString(1),
            FetchedUtc = DateTimeOffset.Parse(reader.GetString(2)),
            ExpiresUtc = DateTimeOffset.Parse(reader.GetString(3))
        };
    }

    public async Task SaveProfileAsync(string cacheKey, int playerId, string templateKey, string? host, string? opposition, string? team, string? span, string payloadJson, string sourceFingerprint, DateTimeOffset fetchedUtc, DateTimeOffset expiresUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO profile_cache (
                cache_key, player_id, template_key, host, opposition, team, span,
                profile_json, source_fingerprint, fetched_utc, expires_utc)
            VALUES (
                $cacheKey, $playerId, $templateKey, $host, $opposition, $team, $span,
                $payloadJson, $sourceFingerprint, $fetchedUtc, $expiresUtc)
            ON CONFLICT(cache_key) DO UPDATE SET
                player_id = excluded.player_id,
                template_key = excluded.template_key,
                host = excluded.host,
                opposition = excluded.opposition,
                team = excluded.team,
                span = excluded.span,
                profile_json = excluded.profile_json,
                source_fingerprint = excluded.source_fingerprint,
                fetched_utc = excluded.fetched_utc,
                expires_utc = excluded.expires_utc;
            """;
        command.Parameters.AddWithValue("$cacheKey", cacheKey);
        command.Parameters.AddWithValue("$playerId", playerId);
        command.Parameters.AddWithValue("$templateKey", templateKey);
        command.Parameters.AddWithValue("$host", (object?)host ?? DBNull.Value);
        command.Parameters.AddWithValue("$opposition", (object?)opposition ?? DBNull.Value);
        command.Parameters.AddWithValue("$team", (object?)team ?? DBNull.Value);
        command.Parameters.AddWithValue("$span", (object?)span ?? DBNull.Value);
        command.Parameters.AddWithValue("$payloadJson", payloadJson);
        command.Parameters.AddWithValue("$sourceFingerprint", sourceFingerprint);
        command.Parameters.AddWithValue("$fetchedUtc", fetchedUtc.ToString("O"));
        command.Parameters.AddWithValue("$expiresUtc", expiresUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<PlayerCacheRecord?> TryGetPlayerAsync(int playerId, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT player_id, display_name, team_code, is_retired, last_match_date,
                   test_runs, odi_runs, t20i_runs, test_wickets, odi_wickets, t20i_wickets,
                   last_profile_check_utc, updated_utc
            FROM players
            WHERE player_id = $playerId
            LIMIT 1;
            """;
        command.Parameters.AddWithValue("$playerId", playerId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new PlayerCacheRecord
        {
            PlayerId = reader.GetInt32(0),
            DisplayName = reader.GetString(1),
            TeamCode = reader.IsDBNull(2) ? null : reader.GetString(2),
            IsRetired = reader.IsDBNull(3) ? null : reader.GetInt32(3) == 1,
            LastMatchDate = reader.IsDBNull(4) ? null : DateOnly.Parse(reader.GetString(4)),
            TestRuns = reader.IsDBNull(5) ? null : reader.GetInt32(5),
            OdiRuns = reader.IsDBNull(6) ? null : reader.GetInt32(6),
            T20iRuns = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            TestWickets = reader.IsDBNull(8) ? null : reader.GetInt32(8),
            OdiWickets = reader.IsDBNull(9) ? null : reader.GetInt32(9),
            T20iWickets = reader.IsDBNull(10) ? null : reader.GetInt32(10),
            LastProfileCheckUtc = reader.IsDBNull(11) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(11)),
            UpdatedUtc = reader.IsDBNull(12) ? DateTimeOffset.MinValue : DateTimeOffset.Parse(reader.GetString(12))
        };
    }

    public async Task SavePlayerAsync(PlayerCacheRecord player, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO players (
                player_id, display_name, team_code, is_retired, last_match_date,
                test_runs, odi_runs, t20i_runs, test_wickets, odi_wickets, t20i_wickets,
                last_profile_check_utc, updated_utc)
            VALUES (
                $playerId, $displayName, $teamCode, $isRetired, $lastMatchDate,
                $testRuns, $odiRuns, $t20iRuns, $testWickets, $odiWickets, $t20iWickets,
                $lastProfileCheckUtc, $updatedUtc)
            ON CONFLICT(player_id) DO UPDATE SET
                display_name = excluded.display_name,
                team_code = COALESCE(excluded.team_code, players.team_code),
                is_retired = COALESCE(excluded.is_retired, players.is_retired),
                last_match_date = COALESCE(excluded.last_match_date, players.last_match_date),
                test_runs = COALESCE(excluded.test_runs, players.test_runs),
                odi_runs = COALESCE(excluded.odi_runs, players.odi_runs),
                t20i_runs = COALESCE(excluded.t20i_runs, players.t20i_runs),
                test_wickets = COALESCE(excluded.test_wickets, players.test_wickets),
                odi_wickets = COALESCE(excluded.odi_wickets, players.odi_wickets),
                t20i_wickets = COALESCE(excluded.t20i_wickets, players.t20i_wickets),
                last_profile_check_utc = COALESCE(excluded.last_profile_check_utc, players.last_profile_check_utc),
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$playerId", player.PlayerId);
        command.Parameters.AddWithValue("$displayName", player.DisplayName);
        command.Parameters.AddWithValue("$teamCode", DbValue(player.TeamCode));
        command.Parameters.AddWithValue("$isRetired", DbValue(player.IsRetired.HasValue ? (int?)(player.IsRetired.Value ? 1 : 0) : null));
        command.Parameters.AddWithValue("$lastMatchDate", DbValue(player.LastMatchDate?.ToString("yyyy-MM-dd")));
        command.Parameters.AddWithValue("$testRuns", DbValue(player.TestRuns));
        command.Parameters.AddWithValue("$odiRuns", DbValue(player.OdiRuns));
        command.Parameters.AddWithValue("$t20iRuns", DbValue(player.T20iRuns));
        command.Parameters.AddWithValue("$testWickets", DbValue(player.TestWickets));
        command.Parameters.AddWithValue("$odiWickets", DbValue(player.OdiWickets));
        command.Parameters.AddWithValue("$t20iWickets", DbValue(player.T20iWickets));
        command.Parameters.AddWithValue("$lastProfileCheckUtc", DbValue(player.LastProfileCheckUtc == DateTimeOffset.MinValue ? null : player.LastProfileCheckUtc.ToString("O")));
        command.Parameters.AddWithValue("$updatedUtc", player.UpdatedUtc == DateTimeOffset.MinValue ? DateTimeOffset.UtcNow.ToString("O") : player.UpdatedUtc.ToString("O"));
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<LeaderboardSnapshotMetadata?> TryGetLatestLeaderboardSnapshotAsync(string format, string discipline, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var snapshotCommand = connection.CreateCommand();
        snapshotCommand.CommandText =
            """
            SELECT snapshot_key, captured_utc
            FROM leaderboard_snapshots
            WHERE format = $format AND discipline = $discipline
            ORDER BY captured_utc DESC
            LIMIT 1;
            """;
        snapshotCommand.Parameters.AddWithValue("$format", format);
        snapshotCommand.Parameters.AddWithValue("$discipline", discipline);

        await using var snapshotReader = await snapshotCommand.ExecuteReaderAsync(cancellationToken);
        if (!await snapshotReader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var snapshotKey = snapshotReader.GetString(0);
        var capturedUtc = DateTimeOffset.Parse(snapshotReader.GetString(1));

        await snapshotReader.DisposeAsync();

        await using var rowsCommand = connection.CreateCommand();
        rowsCommand.CommandText =
            """
            SELECT rank, player_id, total_value
            FROM leaderboard_snapshots
            WHERE snapshot_key = $snapshotKey
            ORDER BY rank ASC;
            """;
        rowsCommand.Parameters.AddWithValue("$snapshotKey", snapshotKey);

        var rows = new List<LeaderboardSnapshotRow>();
        await using var rowReader = await rowsCommand.ExecuteReaderAsync(cancellationToken);
        while (await rowReader.ReadAsync(cancellationToken))
        {
            var playerId = rowReader.GetInt32(1);
            var player = await TryGetPlayerAsync(playerId, cancellationToken);
            rows.Add(new LeaderboardSnapshotRow
            {
                Rank = rowReader.GetInt32(0),
                PlayerId = playerId,
                DisplayName = player?.DisplayName ?? $"Player {playerId}",
                TotalValue = rowReader.GetInt32(2)
            });
        }

        return new LeaderboardSnapshotMetadata
        {
            SnapshotKey = snapshotKey,
            CapturedUtc = capturedUtc,
            Rows = rows
        };
    }

    public async Task SaveLeaderboardSnapshotAsync(string snapshotKey, string format, string discipline, IReadOnlyList<LeaderboardSnapshotRow> rows, DateTimeOffset capturedUtc, CancellationToken cancellationToken)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken);
        await using var transaction = (SqliteTransaction)await connection.BeginTransactionAsync(cancellationToken);

        await using (var deleteCommand = connection.CreateCommand())
        {
            deleteCommand.Transaction = transaction;
            deleteCommand.CommandText =
                """
                DELETE FROM leaderboard_snapshots
                WHERE format = $format AND discipline = $discipline AND snapshot_key <> $snapshotKey;
                """;
            deleteCommand.Parameters.AddWithValue("$format", format);
            deleteCommand.Parameters.AddWithValue("$discipline", discipline);
            deleteCommand.Parameters.AddWithValue("$snapshotKey", snapshotKey);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        foreach (var row in rows)
        {
            await using var insertCommand = connection.CreateCommand();
            insertCommand.Transaction = transaction;
            insertCommand.CommandText =
                """
                INSERT INTO leaderboard_snapshots (
                    snapshot_key, format, discipline, rank, player_id, total_value, captured_utc)
                VALUES (
                    $snapshotKey, $format, $discipline, $rank, $playerId, $totalValue, $capturedUtc);
                """;
            insertCommand.Parameters.AddWithValue("$snapshotKey", snapshotKey);
            insertCommand.Parameters.AddWithValue("$format", format);
            insertCommand.Parameters.AddWithValue("$discipline", discipline);
            insertCommand.Parameters.AddWithValue("$rank", row.Rank);
            insertCommand.Parameters.AddWithValue("$playerId", row.PlayerId);
            insertCommand.Parameters.AddWithValue("$totalValue", row.TotalValue);
            insertCommand.Parameters.AddWithValue("$capturedUtc", capturedUtc.ToString("O"));
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private void EnsureSchema()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText =
            """
            PRAGMA journal_mode = WAL;

            CREATE TABLE IF NOT EXISTS players (
                player_id INTEGER PRIMARY KEY,
                display_name TEXT NOT NULL,
                team_code TEXT NULL,
                is_retired INTEGER NULL,
                last_match_date TEXT NULL,
                test_runs INTEGER NULL,
                odi_runs INTEGER NULL,
                t20i_runs INTEGER NULL,
                test_wickets INTEGER NULL,
                odi_wickets INTEGER NULL,
                t20i_wickets INTEGER NULL,
                last_profile_check_utc TEXT NULL,
                updated_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS url_cache (
                url TEXT PRIMARY KEY,
                response_html TEXT NOT NULL,
                fetched_utc TEXT NOT NULL,
                expires_utc TEXT NOT NULL
            );

            CREATE TABLE IF NOT EXISTS profile_cache (
                cache_key TEXT PRIMARY KEY,
                player_id INTEGER NOT NULL,
                template_key TEXT NOT NULL,
                host TEXT NULL,
                opposition TEXT NULL,
                team TEXT NULL,
                span TEXT NULL,
                profile_json TEXT NOT NULL,
                source_fingerprint TEXT NOT NULL,
                fetched_utc TEXT NOT NULL,
                expires_utc TEXT NOT NULL,
                FOREIGN KEY(player_id) REFERENCES players(player_id)
            );

            CREATE TABLE IF NOT EXISTS leaderboard_snapshots (
                snapshot_key TEXT NOT NULL,
                format TEXT NOT NULL,
                discipline TEXT NOT NULL,
                rank INTEGER NOT NULL,
                player_id INTEGER NOT NULL,
                total_value INTEGER NOT NULL,
                captured_utc TEXT NOT NULL,
                PRIMARY KEY(snapshot_key, rank),
                FOREIGN KEY(player_id) REFERENCES players(player_id)
            );

            CREATE INDEX IF NOT EXISTS ix_profile_cache_player_template
            ON profile_cache(player_id, template_key);

            CREATE INDEX IF NOT EXISTS ix_profile_cache_expires_utc
            ON profile_cache(expires_utc);

            CREATE INDEX IF NOT EXISTS ix_url_cache_expires_utc
            ON url_cache(expires_utc);

            CREATE INDEX IF NOT EXISTS ix_leaderboard_snapshots_lookup
            ON leaderboard_snapshots(format, discipline, captured_utc);
            """;
        command.ExecuteNonQuery();
    }

    private async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }

    private static object DbValue<T>(T? value) where T : struct =>
        value.HasValue ? value.Value : DBNull.Value;

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value;
}
