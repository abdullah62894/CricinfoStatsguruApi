namespace CricinfoStatsguruApi.Models;

public sealed class HammyVizRankingResponse
{
    public required string TemplateKey { get; init; }

    public required string TemplateLabel { get; init; }

    public required string Format { get; init; }

    public required string Discipline { get; init; }

    public required int Limit { get; init; }

    public string? SourceUrl { get; init; }

    public required IReadOnlyList<HammyVizRankingRow> Rows { get; init; }
}
