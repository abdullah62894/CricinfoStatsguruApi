namespace CricinfoStatsguruApi.Models;

public sealed class PlayerCompareMetricRow
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public decimal? PlayerAValue { get; init; }

    public decimal? PlayerABaseline { get; init; }

    public decimal? PlayerADelta { get; init; }

    public decimal? PlayerAHammyVizContribution { get; init; }

    public decimal? PlayerBValue { get; init; }

    public decimal? PlayerBBaseline { get; init; }

    public decimal? PlayerBDelta { get; init; }

    public decimal? PlayerBHammyVizContribution { get; init; }
}
