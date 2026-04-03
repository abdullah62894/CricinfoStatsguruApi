namespace CricinfoStatsguruApi.Models;

public sealed class PlayerSearchResponse
{
    public required string RequestedPlayerName { get; init; }

    public required string TemplateKey { get; init; }

    public required IReadOnlyList<PlayerLookupResult> Candidates { get; init; }

    public required bool AutoSelected { get; init; }

    public required PlayerLookupResult? SelectedCandidate { get; init; }
}
