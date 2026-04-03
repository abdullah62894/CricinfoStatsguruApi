namespace CricinfoStatsguruApi.Models;

public sealed class ProfileMetricSnapshot
{
    public decimal? Average { get; init; }

    public decimal? StrikeRate { get; init; }

    public decimal? BaselineAverage { get; init; }

    public decimal? BaselineStrikeRate { get; init; }

    public decimal? AverageDelta { get; init; }

    public decimal? AverageDeltaPercent { get; init; }

    public decimal? StrikeRateDelta { get; init; }

    public decimal? StrikeRateDeltaPercent { get; init; }

    public decimal? HammyVizContribution { get; init; }

    public decimal? HammyVizWeightPercent { get; init; }

    public int? Matches { get; init; }

    public int? Innings { get; init; }

    public int? DismissalsOrWickets { get; init; }

    public int? RunsOrConceded { get; init; }
}
