namespace CricinfoStatsguruApi.Services;

public sealed class AmbiguousPlayerSearchException(string playerName, IReadOnlyList<string> candidates)
    : Exception($"Multiple Statsguru players matched '{playerName}'. Try a more specific name. Matches: {string.Join(", ", candidates.Take(5))}")
{
}
