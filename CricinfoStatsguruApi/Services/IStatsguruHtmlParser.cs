using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public interface IStatsguruHtmlParser
{
    StatsTable Parse(string html);

    IReadOnlyList<PlayerLookupResult> ParsePlayerSearchResults(string html);
}
