namespace CricinfoStatsguruApi.Models;

public sealed class StatsTable
{
    public required IReadOnlyList<string> Headers { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, string>> Rows { get; init; }
}
