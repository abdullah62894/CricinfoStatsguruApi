namespace CricinfoStatsguruApi.Models;

public sealed class LeaderboardSnapshotRow
{
    public required int Rank { get; init; }

    public required int PlayerId { get; init; }

    public required string DisplayName { get; init; }

    public required int TotalValue { get; init; }
}
