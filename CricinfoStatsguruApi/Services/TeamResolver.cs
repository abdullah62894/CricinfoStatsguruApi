namespace CricinfoStatsguruApi.Services;

public static class TeamResolver
{
    private static readonly int[] AllInternationalTeamIds =
    [
        1, 2, 3, 4, 5, 6, 7, 8, 9, 25, 29, 40, 140
    ];

    private static readonly IReadOnlyDictionary<string, (int TeamId, string TeamName)> Teams =
        new Dictionary<string, (int TeamId, string TeamName)>(StringComparer.OrdinalIgnoreCase)
        {
            ["AFG"] = (40, "Afghanistan"),
            ["AUS"] = (2, "Australia"),
            ["BAN"] = (25, "Bangladesh"),
            ["ENG"] = (1, "England"),
            ["IND"] = (6, "India"),
            ["IRE"] = (29, "Ireland"),
            ["NZ"] = (5, "New Zealand"),
            ["PAK"] = (7, "Pakistan"),
            ["SA"] = (3, "South Africa"),
            ["SL"] = (8, "Sri Lanka"),
            ["WI"] = (4, "West Indies"),
            ["ZIM"] = (9, "Zimbabwe"),
            ["UAE"] = (27, "UAE"),
            ["USA"] = (11, "USA"),
            ["NEP"] = (33, "Nepal"),
            ["SCO"] = (30, "Scotland"),
            ["NED"] = (15, "Netherlands")
        };

    public static (int TeamId, string TeamName)? ResolveFromDisplayName(string displayName)
    {
        var openIndex = displayName.LastIndexOf('(');
        var closeIndex = displayName.LastIndexOf(')');
        if (openIndex < 0 || closeIndex <= openIndex)
        {
            return null;
        }

        var code = displayName[(openIndex + 1)..closeIndex].Trim();
        if (Teams.TryGetValue(code, out var team))
        {
            return team;
        }

        var normalizedCode = code.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        return normalizedCode is not null && Teams.TryGetValue(normalizedCode, out team) ? team : null;
    }

    public static IReadOnlyList<int> GetOppositionTeamIds(int ownTeamId) =>
        AllInternationalTeamIds.Where(teamId => teamId != ownTeamId).ToList();
}
