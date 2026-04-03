namespace CricinfoStatsguruApi.Services;

public sealed class PlayerSearchMissException(string playerName)
    : Exception($"No Statsguru player match was found for '{playerName}'.")
{
}
