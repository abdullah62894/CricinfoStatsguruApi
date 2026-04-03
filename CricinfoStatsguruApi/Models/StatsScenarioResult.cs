namespace CricinfoStatsguruApi.Models;

public sealed class StatsScenarioResult
{
    public required StatsSourceLinks SourceLinks { get; init; }

    public required StatsTable OverallTable { get; init; }

    public required StatsTable IndividualTable { get; init; }
}
