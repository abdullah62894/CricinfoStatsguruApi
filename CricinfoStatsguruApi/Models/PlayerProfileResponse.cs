namespace CricinfoStatsguruApi.Models;

public sealed class PlayerProfileResponse
{
    public required string RequestedPlayerName { get; init; }

    public required string MatchedPlayerName { get; init; }

    public required int PlayerId { get; init; }

    public required string TemplateKey { get; init; }

    public required string TemplateLabel { get; init; }

    public required string Format { get; init; }

    public required string Discipline { get; init; }

    public required int? TeamId { get; init; }

    public required string? TeamName { get; init; }

    public bool ServedFromCache { get; init; }

    public DateTimeOffset? CachedAtUtc { get; init; }

    public decimal? HammyVizScore { get; init; }

    public decimal? HammyVizBlockWeight { get; init; }

    public string? HammyVizFormula { get; init; }

    public required IReadOnlyList<ProfileComparisonResult> Comparisons { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }
}
