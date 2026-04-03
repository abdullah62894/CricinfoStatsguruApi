namespace CricinfoStatsguruApi.Models;

public sealed class StatsResponse
{
    public required string RequestedPlayerName { get; init; }

    public required string MatchedPlayerName { get; init; }

    public required int PlayerId { get; init; }

    public required bool PlayerIdFromCache { get; init; }

    public required string TemplateKey { get; init; }

    public required string TemplateLabel { get; init; }

    public required string Format { get; init; }

    public required string Discipline { get; init; }

    public required IReadOnlyList<string> Metrics { get; init; }

    public required IReadOnlyList<string> ComparisonGroups { get; init; }

    public required IReadOnlyList<QueryFilter> AppliedFilters { get; init; }

    public required StatsSourceLinks SourceLinks { get; init; }

    public required StatsTable OverallTable { get; init; }

    public required StatsTable IndividualTable { get; init; }

    public required IReadOnlyList<PlayerLookupResult> SearchCandidates { get; init; }

    public required string Note { get; init; }
}
