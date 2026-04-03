namespace CricinfoStatsguruApi.Options;

public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    public string DatabasePath { get; set; } = "Data/cache.db";

    public int UrlCacheHours { get; set; } = 24;

    public int ProfileCacheHours { get; set; } = 24;

    public int RetiredProfileCacheHours { get; set; } = 24 * 30;
}
