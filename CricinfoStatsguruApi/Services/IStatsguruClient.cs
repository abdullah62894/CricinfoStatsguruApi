namespace CricinfoStatsguruApi.Services;

public interface IStatsguruClient
{
    Task<string> GetHtmlAsync(string url, CancellationToken cancellationToken, bool bypassCache = false);
}
