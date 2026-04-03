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

        var leaderboardQuery = new StatsQuery
        {
            PlayerName = string.Empty,
            TemplateKey = templateKey,
            ForceRefresh = true
        };
        var totalColumn = GetTotalColumn(template);
        var rows = await FetchLeaderboardRowsAsync(template, leaderboardQuery, totalColumn, limit, cancellationToken);

        var previousSnapshot = await _cacheRepository.TryGetLatestLeaderboardSnapshotAsync(template.Format, template.Discipline, cancellationToken);
        var previousTotals = previousSnapshot?.Rows.ToDictionary(row => row.PlayerId, row => row.TotalValue)
            ?? new Dictionary<int, int>();

        var changedRows = rows
            .Where(row => !previousTotals.TryGetValue(row.PlayerId, out var previousValue) || previousValue != row.TotalValue)
            .ToList();

        var capturedUtc = DateTimeOffset.UtcNow;
        var snapshotKey = $"{template.Key}:{capturedUtc:yyyyMMddHHmmss}";

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
                SelectedPlayerId = changedRow.PlayerId,
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

    public async Task<HammyVizRankingResponse> GetHammyVizRankingsAsync(string templateKey, int limit, string? hostGroup, CancellationToken cancellationToken)
    {
        if (limit <= 0 || limit > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 100.");
        }

        var template = _templateRepository.GetByKey(templateKey)
            ?? throw new KeyNotFoundException($"Unknown template key '{templateKey}'.");

        var snapshot = await _cacheRepository.TryGetLatestLeaderboardSnapshotAsync(template.Format, template.Discipline, cancellationToken);
        if (snapshot is null || snapshot.Rows.Count < limit)
        {
            await WarmLeaderboardAsync(templateKey, limit, cancellationToken);
            snapshot = await _cacheRepository.TryGetLatestLeaderboardSnapshotAsync(template.Format, template.Discipline, cancellationToken);
        }

        if (snapshot is null)
        {
            throw new InvalidOperationException("Unable to build rankings because no leaderboard snapshot is available.");
        }

        var profiles = new List<PlayerProfileResponse>();
        foreach (var row in snapshot.Rows.Take(limit))
        {
            var query = new StatsQuery
            {
                PlayerName = row.DisplayName,
                SelectedPlayerId = row.PlayerId,
                TemplateKey = template.Key,
                HostGroup = hostGroup
            };

            profiles.Add(await _statsService.GetPlayerProfileAsync(query, cancellationToken));
        }

        var ranked = profiles
            .OrderByDescending(profile => profile.HammyVizScore ?? decimal.MinValue)
            .ThenByDescending(profile => profile.Comparisons.FirstOrDefault(comparison => comparison.Key == "player-overall")?.Metrics.Average ?? decimal.MinValue)
            .Select((profile, index) => new HammyVizRankingRow
            {
                Rank = index + 1,
                PlayerId = profile.PlayerId,
                PlayerName = profile.MatchedPlayerName,
                HammyVizScore = profile.HammyVizScore,
                RawAverage = profile.Comparisons.FirstOrDefault(comparison => comparison.Key == "player-overall")?.SubjectMetrics.Average,
                OverallDelta = FindDelta(profile, "all-involved-overall"),
                PaceDelta = FindDelta(profile, "player-vs-pace"),
                SpinDelta = FindDelta(profile, "player-vs-spin"),
                Top7Delta = FindDelta(profile, "own-team-top7") ?? FindDelta(profile, "opposition-top7"),
                SenaDelta = FindDelta(profile, "player-sena"),
                ToughestHostsDelta = FindDelta(profile, "player-toughest-hosts"),
                ToughestHostsTop7Delta = FindDelta(profile, "toughest-hosts-top7")
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

    private async Task<List<LeaderboardSnapshotRow>> FetchLeaderboardRowsAsync(
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

        return leaderboardTable.Rows
            .Select((row, index) => BuildSnapshotRow(row, index + 1, totalColumn))
            .Where(row => row is not null)
            .Cast<LeaderboardSnapshotRow>()
            .Take(limit)
            .ToList();
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
}
