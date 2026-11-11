using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public sealed class CacheWarmupService(
    IFilterTemplateRepository templateRepository,
    IStatsguruUrlBuilder urlBuilder,
    IStatsguruClient client,
    IStatsguruHtmlParser parser,
    IStatsCacheRepository cacheRepository,
    IStatsService statsService) : ICacheWarmupService
{
    private readonly IFilterTemplateRepository _templateRepository = templateRepository;
    private readonly IStatsguruUrlBuilder _urlBuilder = urlBuilder;
    private readonly IStatsguruClient _client = client;
    private readonly IStatsguruHtmlParser _parser = parser;
    private readonly IStatsCacheRepository _cacheRepository = cacheRepository;
    private readonly IStatsService _statsService = statsService;

    public async Task<LeaderboardWarmupResponse> WarmLeaderboardAsync(string templateKey, int limit, CancellationToken cancellationToken)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be greater than zero.");
        }

        var template = _templateRepository.GetByKey(templateKey)
            ?? throw new KeyNotFoundException($"Unknown template key '{templateKey}'.");

        var leaderboardQuery = BuildLeaderboardQuery(templateKey);
        var totalColumn = GetTotalColumn(template);
        var (rows, _) = await FetchLeaderboardRowsAsync(template, leaderboardQuery, totalColumn, limit, cancellationToken);

        var previousSnapshot = await _cacheRepository.TryGetLatestLeaderboardSnapshotAsync(template.Format, template.Discipline, cancellationToken);
        var changedRows = FindChangedRows(rows, previousSnapshot);

        var capturedUtc = DateTimeOffset.UtcNow;
        var snapshotKey = BuildSnapshotKey(template, capturedUtc);

        foreach (var row in rows)
        {
            await UpsertLeaderboardPlayerAsync(template, row, capturedUtc, cancellationToken);
        }

        await _cacheRepository.SaveLeaderboardSnapshotAsync(
            snapshotKey,
            template.Format,
            template.Discipline,
            rows,
            capturedUtc,
            cancellationToken);

        var refreshedProfiles = 0;
        foreach (var changedRow in changedRows)
        {
            var query = new StatsQuery
            {
                PlayerName = changedRow.DisplayName,
                TemplateKey = template.Key,
                ForceRefresh = true
            };

            await _statsService.GetPlayerProfileAsync(query, cancellationToken);
            refreshedProfiles++;
        }

        return new LeaderboardWarmupResponse
        {
            TemplateKey = template.Key,
            Format = template.Format,
            Discipline = template.Discipline,
            RequestedLimit = limit,
            SnapshotSize = rows.Count,
            RefreshedProfiles = refreshedProfiles,
            UnchangedProfiles = rows.Count - changedRows.Count,
            SnapshotKey = snapshotKey,
            ChangedPlayers = changedRows
        };
    }

    public async Task<HammyVizRankingResponse> GetHammyVizRankingsAsync(string templateKey, int limit, string? hostGroup, string? bowlerType, CancellationToken cancellationToken)
    {
        if (limit <= 0 || limit > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        var template = _templateRepository.GetByKey(templateKey)
            ?? throw new KeyNotFoundException($"Unknown template key '{templateKey}'.");

        var leaderboardQuery = BuildLeaderboardQuery(templateKey);
        var totalColumn = GetTotalColumn(template);
        var (leaderboardRows, sourceUrl) = await FetchLeaderboardRowsAsync(template, leaderboardQuery, totalColumn, limit, cancellationToken);

        if (leaderboardRows.Count == 0)
        {
            throw new InvalidOperationException("Unable to build rankings because no rows were found in the Statsguru leaderboard.");
        }

        var previousSnapshot = await _cacheRepository.TryGetLatestLeaderboardSnapshotAsync(template.Format, template.Discipline, cancellationToken);
        var changedRows = FindChangedRows(leaderboardRows, previousSnapshot);
        var changedPlayerIds = changedRows.Select(row => row.PlayerId).ToHashSet();
        var capturedUtc = DateTimeOffset.UtcNow;

        foreach (var row in leaderboardRows)
        {
            await UpsertLeaderboardPlayerAsync(template, row, capturedUtc, cancellationToken);
        }

        await _cacheRepository.SaveLeaderboardSnapshotAsync(
            BuildSnapshotKey(template, capturedUtc),
            template.Format,
            template.Discipline,
            leaderboardRows,
            capturedUtc,
            cancellationToken);

        var profiles = new List<(LeaderboardSnapshotRow Row, PlayerProfileResponse Profile)>();
        foreach (var row in leaderboardRows.Take(limit))
        {
            try
            {
                var forceRefresh = changedPlayerIds.Contains(row.PlayerId);
                var query = new StatsQuery
                {
                    PlayerName = row.DisplayName,
                    TemplateKey = template.Key,
                    HostGroup = hostGroup,
                    BowlerType = bowlerType,
                    ForceRefresh = forceRefresh
                };

                var profile = await _statsService.GetPlayerProfileAsync(query, cancellationToken);
                if (!forceRefresh && NeedsRankingRefresh(profile))
                {
                    profile = await _statsService.GetPlayerProfileAsync(
                        new StatsQuery
                        {
                            PlayerName = row.DisplayName,
                            TemplateKey = template.Key,
                            HostGroup = hostGroup,
                            BowlerType = bowlerType,
                            ForceRefresh = true
                        },
                        cancellationToken);
                }

                profiles.Add((row, profile));
            }
            catch (Exception)
            {
                // If a specific profile fails to load from Statsguru, skip it for the rankings
                // but continue building the rest of the list.
            }
        }

        var ranked = profiles
            .OrderByDescending(item => item.Profile.HammyVizScore ?? decimal.MinValue)
            .ThenBy(item => item.Row.Rank)
            .Select((item, index) => new HammyVizRankingRow
            {
                Rank = index + 1,
                SourceRank = item.Row.Rank,
                SourceTotal = item.Row.TotalValue,
                PlayerId = item.Profile.PlayerId,
                PlayerName = item.Profile.MatchedPlayerName,
                HammyVizScore = item.Profile.HammyVizScore,
                RawAverage = item.Profile.Comparisons.FirstOrDefault(comparison => comparison.Key == "player-overall")?.SubjectMetrics.Average,
                OverallDelta = FindDelta(item.Profile, "all-involved-overall"),
                PaceDelta = FindDelta(item.Profile, "player-vs-pace"),
                SpinDelta = FindDelta(item.Profile, "player-vs-spin"),
                Top7Delta = FindDelta(item.Profile, "own-team-top7") ?? FindDelta(item.Profile, "opposition-top7"),
                SenaDelta = FindDelta(item.Profile, "player-sena"),
                ToughestHostsDelta = FindDelta(item.Profile, "player-toughest-hosts"),
                ToughestHostsTop7Delta = FindDelta(item.Profile, "toughest-hosts-top7")
            })
            .Take(limit)
            .ToList();

        return new HammyVizRankingResponse
        {
            TemplateKey = template.Key,
            TemplateLabel = template.Label,
            Format = template.Format,
            Discipline = template.Discipline,
            Limit = limit,
            SourceUrl = sourceUrl,
            Rows = ranked
        };
    }

    private async Task UpsertLeaderboardPlayerAsync(
        FilterTemplate template,
        LeaderboardSnapshotRow row,
        DateTimeOffset capturedUtc,
        CancellationToken cancellationToken)
    {
        var existing = await _cacheRepository.TryGetPlayerAsync(row.PlayerId, cancellationToken);

        await _cacheRepository.SavePlayerAsync(
            new PlayerCacheRecord
            {
                PlayerId = row.PlayerId,
                DisplayName = existing?.DisplayName ?? row.DisplayName,
                TeamCode = existing?.TeamCode,
                IsRetired = existing?.IsRetired,
                LastMatchDate = existing?.LastMatchDate,
                TestRuns = ResolveRuns(template, existing, row),
                OdiRuns = ResolveOdiRuns(template, existing, row),
                T20iRuns = ResolveT20iRuns(template, existing, row),
                TestWickets = ResolveTestWickets(template, existing, row),
                OdiWickets = ResolveOdiWickets(template, existing, row),
                T20iWickets = ResolveT20iWickets(template, existing, row),
                LastProfileCheckUtc = existing?.LastProfileCheckUtc ?? DateTimeOffset.MinValue,
                UpdatedUtc = capturedUtc
            },
            cancellationToken);
    }

    private async Task<(List<LeaderboardSnapshotRow> Rows, string SourceUrl)> FetchLeaderboardRowsAsync(
        FilterTemplate template,
        StatsQuery leaderboardQuery,
        string totalColumn,
        int limit,
        CancellationToken cancellationToken)
    {
        var requestContext = _urlBuilder.Build(
            template,
            leaderboardQuery,
            extraFilters:
            [
                new QueryFilter { Key = "size", Value = limit.ToString() }
            ]);

        var leaderboardHtml = await _client.GetHtmlAsync(requestContext.OverallUrl, cancellationToken, bypassCache: true);
        var leaderboardTable = _parser.Parse(leaderboardHtml);

        var rows = leaderboardTable.Rows
            .Select((row, index) => BuildSnapshotRow(row, index + 1, totalColumn))
            .Where(row => row is not null)
            .Cast<LeaderboardSnapshotRow>()
            .Take(limit)
            .ToList();

        return (rows, requestContext.OverallUrl);
    }

    private static LeaderboardSnapshotRow? BuildSnapshotRow(IReadOnlyDictionary<string, string> row, int rank, string totalColumn)
    {
        if (!row.TryGetValue("PlayerId", out var playerIdText) || !int.TryParse(playerIdText, out var playerId))
        {
            return null;
        }

        if (!row.TryGetValue("Player", out var displayName) || string.IsNullOrWhiteSpace(displayName))
        {
            return null;
        }

        if (!row.TryGetValue(totalColumn, out var totalText))
        {
            return null;
        }

        var digitsOnly = new string(totalText.Where(ch => char.IsDigit(ch) || ch == '-').ToArray());
        if (!int.TryParse(digitsOnly, out var totalValue))
        {
            return null;
        }

        return new LeaderboardSnapshotRow
        {
            Rank = rank,
            PlayerId = playerId,
            DisplayName = displayName,
            TotalValue = totalValue
        };
    }

    private static string GetTotalColumn(FilterTemplate template) =>
        string.Equals(template.Discipline, "Batting", StringComparison.OrdinalIgnoreCase)
            ? "Runs"
            : "Wkts";

    private static StatsQuery BuildLeaderboardQuery(string templateKey) =>
        new()
        {
            PlayerName = string.Empty,
            TemplateKey = templateKey,
            ForceRefresh = true
        };

    private static string BuildSnapshotKey(FilterTemplate template, DateTimeOffset capturedUtc) =>
        $"{template.Key}:{capturedUtc:yyyyMMddHHmmssfff}";

    private static List<LeaderboardSnapshotRow> FindChangedRows(
        IReadOnlyList<LeaderboardSnapshotRow> rows,
        LeaderboardSnapshotMetadata? previousSnapshot)
    {
        var previousTotals = previousSnapshot?.Rows.ToDictionary(row => row.PlayerId, row => row.TotalValue)
            ?? new Dictionary<int, int>();

        return rows
            .Where(row => !previousTotals.TryGetValue(row.PlayerId, out var previousValue) || previousValue != row.TotalValue)
            .ToList();
    }

    private static int? ResolveRuns(FilterTemplate template, PlayerCacheRecord? existing, LeaderboardSnapshotRow row) =>
        string.Equals(template.Key, "test-batting-profile", StringComparison.OrdinalIgnoreCase)
            ? row.TotalValue
            : existing?.TestRuns;

    private static int? ResolveOdiRuns(FilterTemplate template, PlayerCacheRecord? existing, LeaderboardSnapshotRow row) =>
        string.Equals(template.Key, "odi-batting-profile", StringComparison.OrdinalIgnoreCase)
            ? row.TotalValue
            : existing?.OdiRuns;

    private static int? ResolveT20iRuns(FilterTemplate template, PlayerCacheRecord? existing, LeaderboardSnapshotRow row) =>
        string.Equals(template.Key, "t20i-batting-profile", StringComparison.OrdinalIgnoreCase)
            ? row.TotalValue
            : existing?.T20iRuns;

    private static int? ResolveTestWickets(FilterTemplate template, PlayerCacheRecord? existing, LeaderboardSnapshotRow row) =>
        string.Equals(template.Key, "test-bowling-profile", StringComparison.OrdinalIgnoreCase)
            ? row.TotalValue
            : existing?.TestWickets;

    private static int? ResolveOdiWickets(FilterTemplate template, PlayerCacheRecord? existing, LeaderboardSnapshotRow row) =>
        string.Equals(template.Key, "odi-bowling-profile", StringComparison.OrdinalIgnoreCase)
            ? row.TotalValue
            : existing?.OdiWickets;

    private static int? ResolveT20iWickets(FilterTemplate template, PlayerCacheRecord? existing, LeaderboardSnapshotRow row) =>
        string.Equals(template.Key, "t20i-bowling-profile", StringComparison.OrdinalIgnoreCase)
            ? row.TotalValue
            : existing?.T20iWickets;

    private static decimal? FindDelta(PlayerProfileResponse profile, string key) =>
        profile.Comparisons.FirstOrDefault(comparison => string.Equals(comparison.Key, key, StringComparison.OrdinalIgnoreCase))?.Metrics.AverageDelta;

    private static bool NeedsRankingRefresh(PlayerProfileResponse profile) =>
        profile.HammyVizScore is null ||
        !profile.Comparisons.Any(comparison => string.Equals(comparison.Key, "player-overall", StringComparison.OrdinalIgnoreCase));
}
