namespace CricinfoStatsguruApi.Models;

public sealed class StatsguruRequestContext
{
    public required IReadOnlyList<QueryFilter> AppliedFilters { get; init; }

    public required string SearchUrl { get; init; }

    public required string OverallUrl { get; init; }

    public required string IndividualUrl { get; init; }
}
