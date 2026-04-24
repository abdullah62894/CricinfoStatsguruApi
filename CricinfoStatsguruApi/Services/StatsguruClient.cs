using CricinfoStatsguruApi.Options;
using Microsoft.Extensions.Options;
using System.Text.RegularExpressions;

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

        int maxRetries = 3;
        int retryDelayMs = 800;

        for (int i = 0; i <= maxRetries; i++)
        {
            try
            {
                using var response = await _httpClient.GetAsync(url, cancellationToken);
                
                if ((int)response.StatusCode == 503 || (int)response.StatusCode == 429)
                {
                    if (i == maxRetries) response.EnsureSuccessStatusCode();
                    await Task.Delay(retryDelayMs * (i + 1), cancellationToken);
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var simplifiedHtml = SimplifyHtml(html);

                var fetchedUtc = DateTimeOffset.UtcNow;
                await _cacheRepository.SaveUrlAsync(
                    url,
                    simplifiedHtml,
                    fetchedUtc,
                    fetchedUtc.AddHours(_cacheOptions.UrlCacheHours),
                    cancellationToken);

                return simplifiedHtml;
            }
            catch (HttpRequestException) when (i < maxRetries)
            {
                await Task.Delay(retryDelayMs * (i + 1), cancellationToken);
            }
        }

        throw new HttpRequestException("Failed to fetch Statsguru data after retries.");
    }

    private static string SimplifyHtml(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var bodyMatch = Regex.Match(html, "<body\\b[^>]*>(?<body>.*?)</body>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        var simplified = bodyMatch.Success ? bodyMatch.Groups["body"].Value : html;

        simplified = Regex.Replace(simplified, "<!--.*?-->", string.Empty, RegexOptions.Singleline);
        simplified = Regex.Replace(simplified, "<(script|style|noscript|svg|iframe|object)\\b.*?</\\1>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        simplified = Regex.Replace(simplified, "<(img|meta|link)\\b[^>]*>", string.Empty, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        simplified = Regex.Replace(simplified, "\\s+", " ");

        return simplified.Trim();
    }
}
