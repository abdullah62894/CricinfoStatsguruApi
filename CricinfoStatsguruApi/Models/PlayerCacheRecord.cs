namespace CricinfoStatsguruApi.Models;

public sealed class PlayerCacheRecord
{
    public required int PlayerId { get; init; }

    public required string DisplayName { get; init; }

    public string? TeamCode { get; init; }

    public bool? IsRetired { get; init; }

    public DateOnly? LastMatchDate { get; init; }

    public int? TestRuns { get; init; }

    public int? OdiRuns { get; init; }

    public int? T20iRuns { get; init; }

    public int? TestWickets { get; init; }

    public int? OdiWickets { get; init; }

    public int? T20iWickets { get; init; }

    public DateTimeOffset LastProfileCheckUtc { get; init; }

    public DateTimeOffset UpdatedUtc { get; init; }
}
