using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public interface IStatsService
{
    Task<PlayerSearchResponse> SearchPlayersAsync(StatsQuery query, CancellationToken cancellationToken);

    Task<StatsResponse> GetPlayerStatsAsync(StatsQuery query, CancellationToken cancellationToken);

    Task<PlayerProfileResponse> GetPlayerProfileAsync(StatsQuery query, CancellationToken cancellationToken);

    Task<PlayerCompareResponse> ComparePlayersAsync(StatsQuery playerAQuery, StatsQuery playerBQuery, CancellationToken cancellationToken);
}
