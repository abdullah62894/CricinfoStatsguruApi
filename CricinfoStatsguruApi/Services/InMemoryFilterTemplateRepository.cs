using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public sealed class InMemoryFilterTemplateRepository : IFilterTemplateRepository
{
    private static readonly IReadOnlyList<string> BattingComparisonGroups =
    [
        "Player batting average in matches played",
        "Match batting average for all batters in those matches, including own team and opposition",
        "Opposition batters average in those matches, excluding the player's own team",
        "Player team batters average in those matches, including the player where applicable",
        "Top 7 player-team batters average in those matches",
        "Top 7 opposition batters average in those matches",
        "Top 7 batters in toughest 5 hosts"
    ];

    private static readonly IReadOnlyList<string> WhiteBallBattingMetrics =
    [
        "Batting average",
        "Strike rate"
    ];

    private static readonly IReadOnlyList<string> TestBattingMetrics =
    [
        "Batting average"
    ];

    private static readonly IReadOnlyList<string> BowlingComparisonGroups =
    [
        "Player bowling average in matches played",
        "Match bowling average for all bowlers in those matches, including own team and opposition",
        "Opposition bowlers average in those matches, excluding the player's own team",
        "Player team bowlers average in those matches, including the player where applicable",
        "Player team bowlers average against top-7 batting positions in those matches",
        "Opposition bowlers average against top-7 batting positions in those matches"
    ];

    private static readonly IReadOnlyList<string> WhiteBallBowlingMetrics =
    [
        "Bowling average",
        "Bowling strike rate"
    ];

    private static readonly IReadOnlyList<string> TestBowlingMetrics =
    [
        "Bowling average"
    ];

    private static readonly IReadOnlyList<string> TestBattingNotes =
    [
        "Include SENA split comparisons for Test batting only.",
        "Include Toughest 5 Hosts top-7 benchmark for Test batting.",
        "Do not include strike-rate comparisons in Test batting profiles.",
        "Use only matches the player appeared in when calculating team and opposition comparison averages."
    ];

    private static readonly IReadOnlyList<string> WhiteBallBattingNotes =
    [
        "Do not include SENA-only comparison blocks for ODI or T20 batting profiles.",
        "Include batting strike-rate comparisons for player, team, and opposition where supported by the source.",
        "Include Toughest 5 Hosts top-7 benchmark for white-ball batting.",
        "Use only matches the player appeared in when calculating team and opposition comparison averages."
    ];

    private static readonly IReadOnlyList<string> TestBowlingNotes =
    [
        "Include team and opposition top-7 bowling comparison blocks.",
        "Do not include strike-rate comparisons in Test bowling profiles.",
        "Use only matches the player appeared in when calculating team and opposition comparison averages."
    ];

    private static readonly IReadOnlyList<string> WhiteBallBowlingNotes =
    [
        "Include bowling strike-rate comparisons for ODI and T20 bowling profiles.",
        "Include team and opposition top-7 bowling comparison blocks.",
        "Use only matches the player appeared in when calculating team and opposition comparison averages."
    ];

    private static readonly IReadOnlyList<FilterTemplate> Templates =
    [
        new FilterTemplate
        {
            Key = "test-batting-profile",
            Label = "Test batting comparison profile",
            Format = "Test",
            Discipline = "Batting",
            StatView = "batting",
            Summary = "Compares a Test batter's average against match-wide, own-team, opposition, top-7, SENA, and toughest-host baselines from the matches they played.",
            Metrics = TestBattingMetrics,
            ComparisonGroups =
            [
                .. BattingComparisonGroups,
                "Player batting average in SENA countries",
                "Player team batters average in SENA countries",
                "Opposition batters average in SENA countries"
            ],
            Notes = TestBattingNotes,
            BaseParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["class"] = "1",
                ["filter"] = "advanced",
                ["orderby"] = "runs",
                ["type"] = "batting",
                ["template"] = "results"
            }
        },
        new FilterTemplate
        {
            Key = "odi-batting-profile",
            Label = "ODI batting comparison profile",
            Format = "ODI",
            Discipline = "Batting",
            StatView = "batting",
            Summary = "Compares an ODI batter's average and strike rate against match-wide, own-team, opposition, top-7, and toughest-host baselines from the matches they played.",
            Metrics = WhiteBallBattingMetrics,
            ComparisonGroups = BattingComparisonGroups,
            Notes = WhiteBallBattingNotes,
            BaseParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["class"] = "2",
                ["filter"] = "advanced",
                ["orderby"] = "runs",
                ["type"] = "batting",
                ["template"] = "results"
            }
        },
        new FilterTemplate
        {
            Key = "t20i-batting-profile",
            Label = "T20I batting comparison profile",
            Format = "T20I",
            Discipline = "Batting",
            StatView = "batting",
            Summary = "Compares a T20I batter's average and strike rate against match-wide, own-team, opposition, top-7, and toughest-host baselines from the matches they played.",
            Metrics = WhiteBallBattingMetrics,
            ComparisonGroups = BattingComparisonGroups,
            Notes = WhiteBallBattingNotes,
            BaseParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["class"] = "3",
                ["filter"] = "advanced",
                ["orderby"] = "runs",
                ["type"] = "batting",
                ["template"] = "results"
            }
        },
        new FilterTemplate
        {
            Key = "test-bowling-profile",
            Label = "Test bowling comparison profile",
            Format = "Test",
            Discipline = "Bowling",
            StatView = "bowling",
            Summary = "Compares a Test bowler's bowling average against match-wide, own-team, opposition, top-7 batting-position, and SENA baselines from the matches they played.",
            Metrics = TestBowlingMetrics,
            ComparisonGroups =
            [
                .. BowlingComparisonGroups,
                "Player bowling average in SENA countries",
                "Player team bowlers average in SENA countries",
                "Opposition bowlers average in SENA countries"
            ],
            Notes = TestBowlingNotes,
            BaseParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["class"] = "1",
                ["filter"] = "advanced",
                ["orderby"] = "wickets",
                ["type"] = "bowling",
                ["template"] = "results"
            }
        },
        new FilterTemplate
        {
            Key = "odi-bowling-profile",
            Label = "ODI bowling comparison profile",
            Format = "ODI",
            Discipline = "Bowling",
            StatView = "bowling",
            Summary = "Compares an ODI bowler's bowling average and strike rate against match-wide, own-team, opposition, and top-7 batting-position baselines from the matches they played.",
            Metrics = WhiteBallBowlingMetrics,
            ComparisonGroups = BowlingComparisonGroups,
            Notes = WhiteBallBowlingNotes,
            BaseParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["class"] = "2",
                ["filter"] = "advanced",
                ["orderby"] = "wickets",
                ["type"] = "bowling",
                ["template"] = "results"
            }
        },
        new FilterTemplate
        {
            Key = "t20i-bowling-profile",
            Label = "T20I bowling comparison profile",
            Format = "T20I",
            Discipline = "Bowling",
            StatView = "bowling",
            Summary = "Compares a T20I bowler's bowling average and strike rate against match-wide, own-team, opposition, and top-7 batting-position baselines from the matches they played.",
            Metrics = WhiteBallBowlingMetrics,
            ComparisonGroups = BowlingComparisonGroups,
            Notes = WhiteBallBowlingNotes,
            BaseParameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["class"] = "3",
                ["filter"] = "advanced",
                ["orderby"] = "wickets",
                ["type"] = "bowling",
                ["template"] = "results"
            }
        }
    ];

    public IReadOnlyList<FilterTemplate> GetAll() => Templates;

    public FilterTemplate? GetByKey(string key) =>
        Templates.FirstOrDefault(template => string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase));
}
