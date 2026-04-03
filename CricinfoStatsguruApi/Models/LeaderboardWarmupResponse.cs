namespace CricinfoStatsguruApi.Models;

public sealed class LeaderboardWarmupResponse
{
    public required string TemplateKey { get; init; }

    public required string Format { get; init; }

    public required string Discipline { get; init; }

    public required int RequestedLimit { get; init; }

    public required int SnapshotSize { get; init; }

    public required int RefreshedProfiles { get; init; }

    public required int UnchangedProfiles { get; init; }

    public required string SnapshotKey { get; init; }

    public required IReadOnlyList<LeaderboardSnapshotRow> ChangedPlayers { get; init; }
}
