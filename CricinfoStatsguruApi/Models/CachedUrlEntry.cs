namespace CricinfoStatsguruApi.Models;

public sealed class CachedUrlEntry
{
    public required string Url { get; init; }

    public required string Html { get; init; }

    public required DateTimeOffset FetchedUtc { get; init; }

    public required DateTimeOffset ExpiresUtc { get; init; }
}
