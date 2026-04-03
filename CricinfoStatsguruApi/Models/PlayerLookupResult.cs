namespace CricinfoStatsguruApi.Models;

public sealed class PlayerLookupResult
{
    public required int PlayerId { get; init; }

    public required string DisplayName { get; init; }
}
