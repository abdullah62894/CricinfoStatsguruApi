namespace CricinfoStatsguruApi.Models;

public sealed class ProfileComparisonResult
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Scope { get; init; }

    public required ProfileMetricSnapshot SubjectMetrics { get; init; }

    public required ProfileMetricSnapshot BaselineMetrics { get; init; }

    public required ProfileMetricSnapshot Metrics { get; init; }

    public required StatsSourceLinks SourceLinks { get; init; }

    public string? Note { get; init; }
}
