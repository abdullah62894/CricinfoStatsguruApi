using System.Text.Json;
using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public sealed class FilePlayerLookupCache(IHostEnvironment hostEnvironment) : IPlayerLookupCache
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _cacheFilePath = Path.Combine(hostEnvironment.ContentRootPath, "Data", "player-id-cache.json");
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<PlayerLookupResult?> TryGetAsync(string lookupKey, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cache = await ReadAllUnsafeAsync(cancellationToken);
            return cache.TryGetValue(lookupKey, out var entry)
                ? new PlayerLookupResult
                {
                    PlayerId = entry.PlayerId,
                    DisplayName = entry.DisplayName
                }
                : null;
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SaveAsync(string lookupKey, PlayerLookupResult player, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var cache = await ReadAllUnsafeAsync(cancellationToken);
            cache[lookupKey] = new PlayerIdCacheEntry
            {
                LookupKey = lookupKey,
                PlayerId = player.PlayerId,
                DisplayName = player.DisplayName,
                CachedAtUtc = DateTimeOffset.UtcNow
            };

            var directory = Path.GetDirectoryName(_cacheFilePath)!;
            Directory.CreateDirectory(directory);

            await using var stream = File.Create(_cacheFilePath);
            await JsonSerializer.SerializeAsync(stream, cache, SerializerOptions, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task ClearAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (File.Exists(_cacheFilePath))
            {
                File.Delete(_cacheFilePath);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<Dictionary<string, PlayerIdCacheEntry>> ReadAllUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_cacheFilePath))
        {
            return new Dictionary<string, PlayerIdCacheEntry>(StringComparer.OrdinalIgnoreCase);
        }

        await using var stream = File.OpenRead(_cacheFilePath);
        var cache = await JsonSerializer.DeserializeAsync<Dictionary<string, PlayerIdCacheEntry>>(stream, SerializerOptions, cancellationToken);
        return cache ?? new Dictionary<string, PlayerIdCacheEntry>(StringComparer.OrdinalIgnoreCase);
    }
}
