namespace CricinfoStatsguruApi.Models;

public sealed class PlayerIdCacheEntry
{
    public required string LookupKey { get; init; }

    public required int PlayerId { get; init; }

    public required string DisplayName { get; init; }

    public required DateTimeOffset CachedAtUtc { get; init; }
}
