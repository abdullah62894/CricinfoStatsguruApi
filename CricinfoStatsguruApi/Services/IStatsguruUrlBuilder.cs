using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public interface IStatsguruUrlBuilder
{
    StatsguruRequestContext Build(
        FilterTemplate template,
        StatsQuery query,
        int? playerId = null,
        IReadOnlyList<QueryFilter>? extraFilters = null);
}
