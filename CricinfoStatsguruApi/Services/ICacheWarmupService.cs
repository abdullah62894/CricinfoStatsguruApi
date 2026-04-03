using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public interface ICacheWarmupService
{
    Task<LeaderboardWarmupResponse> WarmLeaderboardAsync(string templateKey, int limit, CancellationToken cancellationToken);

    Task<HammyVizRankingResponse> GetHammyVizRankingsAsync(string templateKey, int limit, string? hostGroup, string? bowlerType, CancellationToken cancellationToken);
}
