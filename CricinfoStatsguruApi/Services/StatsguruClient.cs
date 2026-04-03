using CricinfoStatsguruApi.Options;
using Microsoft.Extensions.Options;

namespace CricinfoStatsguruApi.Services;

public sealed class StatsguruClient(
    HttpClient httpClient,
    IStatsCacheRepository cacheRepository,
    IOptions<CacheOptions> cacheOptions) : IStatsguruClient
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly IStatsCacheRepository _cacheRepository = cacheRepository;
    private readonly CacheOptions _cacheOptions = cacheOptions.Value;

    public async Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken, bool bypassCache = false)
    {
        if (!bypassCache)
        {
            var cached = await _cacheRepository.TryGetUrlAsync(url, cancellationToken);
            if (cached is not null)
            {
                return cached.Html;
            }
        }

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(cancellationToken);

        var fetchedUtc = DateTimeOffset.UtcNow;
        await _cacheRepository.SaveUrlAsync(
            url,
            html,
            fetchedUtc,
            fetchedUtc.AddHours(_cacheOptions.UrlCacheHours),
            cancellationToken);

        return html;
    }
}
