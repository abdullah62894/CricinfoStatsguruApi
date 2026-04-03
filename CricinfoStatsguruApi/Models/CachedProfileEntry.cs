namespace CricinfoStatsguruApi.Models;

public sealed class CachedProfileEntry
{
    public required string CacheKey { get; init; }

    public required string PayloadJson { get; init; }

    public required DateTimeOffset FetchedUtc { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }
}
