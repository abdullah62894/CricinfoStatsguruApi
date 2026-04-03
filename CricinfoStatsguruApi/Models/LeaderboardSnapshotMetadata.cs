namespace CricinfoStatsguruApi.Models;

public sealed class LeaderboardSnapshotMetadata
{
    public required string SnapshotKey { get; init; }

    public required DateTimeOffset CapturedUtc { get; init; }

    public required IReadOnlyList<LeaderboardSnapshotRow> Rows { get; init; }
}
