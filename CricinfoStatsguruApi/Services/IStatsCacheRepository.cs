using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public interface IStatsCacheRepository
{
    Task<CachedUrlEntry?> TryGetUrlAsync(string url, CancellationToken cancellationToken);

    Task SaveUrlAsync(string url, string html, DateTimeOffset fetchedUtc, DateTimeOffset expiresUtc, CancellationToken cancellationToken);

    Task<CachedProfileEntry?> TryGetProfileAsync(string cacheKey, CancellationToken cancellationToken);

    Task SaveProfileAsync(string cacheKey, int playerId, string templateKey, string? host, string? opposition, string? team, string? span, string payloadJson, string sourceFingerprint, DateTimeOffset fetchedUtc, DateTimeOffset expiresUtc, CancellationToken cancellationToken);

    Task<PlayerCacheRecord?> TryGetPlayerAsync(int playerId, CancellationToken cancellationToken);

    Task SavePlayerAsync(PlayerCacheRecord player, CancellationToken cancellationToken);

    Task<LeaderboardSnapshotMetadata?> TryGetLatestLeaderboardSnapshotAsync(string format, string discipline, CancellationToken cancellationToken);

    Task SaveLeaderboardSnapshotAsync(string snapshotKey, string format, string discipline, IReadOnlyList<LeaderboardSnapshotRow> rows, DateTimeOffset capturedUtc, CancellationToken cancellationToken);

    Task ResetAllAsync(CancellationToken cancellationToken);
}
