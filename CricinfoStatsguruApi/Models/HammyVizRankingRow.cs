namespace CricinfoStatsguruApi.Models;

public sealed class HammyVizRankingRow
{
    public required int Rank { get; init; }

    public required int SourceRank { get; init; }

    public required int SourceTotal { get; init; }

    public required int PlayerId { get; init; }

    public required string PlayerName { get; init; }

    public decimal? HammyVizScore { get; init; }

    public decimal? RawAverage { get; init; }

    public decimal? OverallDelta { get; init; }

    public decimal? PaceDelta { get; init; }

    public decimal? SpinDelta { get; init; }

    public decimal? Top7Delta { get; init; }

    public decimal? SenaDelta { get; init; }

    public decimal? ToughestHostsDelta { get; init; }
    public decimal? ToughestHostsTop7Delta { get; init; }
}
