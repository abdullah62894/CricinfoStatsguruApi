namespace CricinfoStatsguruApi.Options;

public sealed class StatsguruOptions
{
    public const string SectionName = "Statsguru";

    public string BaseUrl { get; set; } = "https://stats.espncricinfo.com/ci/engine/stats/index.html";

    public string UserAgent { get; set; } = "Mozilla/5.0 (compatible; CricinfoStatsguruApi/1.0)";
}
