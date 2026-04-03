namespace CricinfoStatsguruApi.Models;

public sealed class PlayerCompareResponse
{
    public required string TemplateKey { get; init; }

    public required string TemplateLabel { get; init; }

    public required string Format { get; init; }

    public required string Discipline { get; init; }

    public required PlayerProfileResponse PlayerA { get; init; }

    public required PlayerProfileResponse PlayerB { get; init; }

    public required IReadOnlyList<PlayerCompareMetricRow> Rows { get; init; }
}
