namespace CricinfoStatsguruApi.Services;

public static class HostResolver
{
    private static readonly IReadOnlyDictionary<string, int> Hosts =
        new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["England"] = 1,
            ["Australia"] = 2,
            ["South Africa"] = 3,
            ["West Indies"] = 4,
            ["New Zealand"] = 5,
            ["India"] = 6,
            ["Pakistan"] = 7,
            ["Sri Lanka"] = 8,
            ["Zimbabwe"] = 9,
            ["Bangladesh"] = 25,
            ["Ireland"] = 29,
            ["Afghanistan"] = 40,
            ["United Arab Emirates"] = 27,
            ["UAE"] = 27,
            ["Scotland"] = 30,
            ["Netherlands"] = 15,
            ["Nepal"] = 33,
            ["USA"] = 11
        };

    public static int? ResolveHostId(string hostName) =>
        Hosts.TryGetValue(hostName.Trim(), out var hostId) ? hostId : null;
}
