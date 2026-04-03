using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public interface IPlayerLookupCache
{
    Task<PlayerLookupResult?> TryGetAsync(string lookupKey, CancellationToken cancellationToken);

    Task SaveAsync(string lookupKey, PlayerLookupResult player, CancellationToken cancellationToken);
}
