using CricinfoStatsguruApi.Models;
using CricinfoStatsguruApi.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace CricinfoStatsguruApi.Services;

public sealed class StatsService(
    IFilterTemplateRepository templateRepository,
    IStatsguruUrlBuilder urlBuilder,
    IStatsguruClient client,
    IStatsguruHtmlParser parser,
    IPlayerLookupCache playerLookupCache,
    IStatsCacheRepository statsCacheRepository,
    IOptions<CacheOptions> cacheOptions) : IStatsService
{
    private static readonly int[] SenaHostIds = [2, 1, 5, 3];
    private static readonly int[] SenaiHostIds = [2, 1, 5, 3, 6];
    private static readonly int[] SwenaHostIds = [2, 1, 5, 3, 4];

    private const string ProfileCacheVersion = "v7";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IFilterTemplateRepository _templateRepository = templateRepository;
    private readonly IStatsguruUrlBuilder _urlBuilder = urlBuilder;
    private readonly IStatsguruClient _client = client;
    private readonly IStatsguruHtmlParser _parser = parser;
    private readonly IPlayerLookupCache _playerLookupCache = playerLookupCache;
    private readonly IStatsCacheRepository _statsCacheRepository = statsCacheRepository;
    private readonly CacheOptions _cacheOptions = cacheOptions.Value;

    public async Task<PlayerSearchResponse> SearchPlayersAsync(StatsQuery query, CancellationToken cancellationToken)
    {
        var template = GetTemplate(query.TemplateKey);
        var candidates = await SearchCandidatesAsync(template, query, cancellationToken);
        var autoSelected = candidates.Count == 1;

        return new PlayerSearchResponse
        {
            RequestedPlayerName = query.PlayerName,
            TemplateKey = template.Key,
            Candidates = candidates,
            AutoSelected = autoSelected,
            SelectedCandidate = autoSelected ? candidates[0] : null
        };
    }

    public async Task<StatsResponse> GetPlayerStatsAsync(StatsQuery query, CancellationToken cancellationToken)
    {
        var template = GetTemplate(query.TemplateKey);
        var (resolvedPlayer, searchCandidates, playerIdFromCache) =
            await ResolvePlayerAsync(template, query, cancellationToken);

        var scenario = await FetchScenarioAsync(template, query, resolvedPlayer.PlayerId, null, cancellationToken);

        return new StatsResponse
        {
            RequestedPlayerName = query.PlayerName,
            MatchedPlayerName = resolvedPlayer.DisplayName,
            PlayerId = resolvedPlayer.PlayerId,
            PlayerIdFromCache = playerIdFromCache,
            TemplateKey = template.Key,
            TemplateLabel = template.Label,
            Format = template.Format,
            Discipline = template.Discipline,
            Metrics = template.Metrics,
            ComparisonGroups = template.ComparisonGroups,
            AppliedFilters = _urlBuilder.Build(template, query, resolvedPlayer.PlayerId).AppliedFilters,
            SourceLinks = scenario.SourceLinks,
            OverallTable = scenario.OverallTable,
            IndividualTable = scenario.IndividualTable,
            SearchCandidates = searchCandidates,
            Note = "Statsguru cumulative comparisons come from the overall query, and innings-level rows come from the individual query. The profile endpoint builds separate comparison blocks from these source tables."
        };
    }

    public async Task<PlayerProfileResponse> GetPlayerProfileAsync(StatsQuery query, CancellationToken cancellationToken)
    {
        var template = GetTemplate(query.TemplateKey);
        var (resolvedPlayer, _, _) = await ResolvePlayerAsync(template, query, cancellationToken);
        var team = TeamResolver.ResolveFromDisplayName(resolvedPlayer.DisplayName);
        await PersistPlayerRecordAsync(resolvedPlayer, cancellationToken);

        var profileCacheKey = BuildProfileCacheKey(template, query, resolvedPlayer.PlayerId);
        var cachedProfile = await _statsCacheRepository.TryGetProfileAsync(profileCacheKey, cancellationToken);
        if (!query.ForceRefresh && cachedProfile is not null)
        {
            try
            {
                var deserialized = JsonSerializer.Deserialize<PlayerProfileResponse>(cachedProfile.PayloadJson, JsonOptions);
                if (deserialized is not null)
                {
                    return new PlayerProfileResponse
                    {
                        RequestedPlayerName = deserialized.RequestedPlayerName,
                        MatchedPlayerName = deserialized.MatchedPlayerName,
                        PlayerId = deserialized.PlayerId,
                        TemplateKey = deserialized.TemplateKey,
                        TemplateLabel = deserialized.TemplateLabel,
                        Format = deserialized.Format,
                        Discipline = deserialized.Discipline,
                        TeamId = deserialized.TeamId,
                        TeamName = deserialized.TeamName,
                        ServedFromCache = true,
                        CachedAtUtc = cachedProfile.FetchedUtc,
                        HammyVizScore = deserialized.HammyVizScore,
                        HammyVizBlockWeight = deserialized.HammyVizBlockWeight,
                        HammyVizFormula = deserialized.HammyVizFormula,
                        Comparisons = deserialized.Comparisons,
                        Notes = deserialized.Notes
                    };
                }
            }
            catch (JsonException)
            {
                // Older cached payloads can be incompatible after response-model changes.
                // Ignore them and regenerate a fresh profile below, which will overwrite the cache entry.
            }
        }

        var comparisons = new List<ProfileComparisonResult>();
        var notes = new List<string>();

        var baseScenario = await FetchScenarioAsync(template, query, resolvedPlayer.PlayerId, null, cancellationToken);
        comparisons.Add(BuildComparison("player-overall", "Player overall", "Player metric in the selected match set", template, resolvedPlayer, baseScenario, baseScenario));
        comparisons.Add(BuildComparison("all-involved-overall", $"{template.Discipline} overall in the same matches", $"Player metric versus the filtered {template.Discipline.ToLowerInvariant()} pool excluding the player", template, resolvedPlayer, baseScenario, baseScenario));

        if (team is null)
        {
            notes.Add("Team-specific comparisons were skipped because the matched player name could not be mapped to a Statsguru team id.");
        }
        else
        {
            var ownTeamFilters = new List<QueryFilter> { new() { Key = "team", Value = team.Value.TeamId.ToString() } };
            var oppositionFilters = BuildOppositionTeamFilters(team.Value.TeamId);

            var ownTeamScenario = await FetchScenarioAsync(template, query, resolvedPlayer.PlayerId, ownTeamFilters, cancellationToken);
            var oppositionScenario = await FetchScenarioAsync(template, query, resolvedPlayer.PlayerId, oppositionFilters, cancellationToken);

            comparisons.Add(BuildComparison("own-team-overall", $"{team.Value.TeamName} overall", $"Player metric versus own-team {template.Discipline.ToLowerInvariant()} baseline excluding the player", template, resolvedPlayer, baseScenario, ownTeamScenario, excludePlayerFromBaseline: true));
            comparisons.Add(BuildComparison("opposition-overall", "Opposition overall", $"Player metric versus opposition {template.Discipline.ToLowerInvariant()} baseline", template, resolvedPlayer, baseScenario, oppositionScenario, excludePlayerFromBaseline: false));

            if (IsBattingTemplate(template))
            {
                await AddBattingComparisonsAsync(
                    comparisons,
                    template,
                    query,
                    resolvedPlayer,
                    baseScenario,
                    team.Value.TeamId,
                    team.Value.TeamName,
                    cancellationToken);
            }
            else
            {
                await AddBowlingComparisonsAsync(
                    comparisons,
                    template,
                    query,
                    resolvedPlayer,
                    baseScenario,
                    team.Value.TeamId,
                    team.Value.TeamName,
                    cancellationToken);
            }
        }

        if (IsBattingTemplate(template))
        {
            await AddToughestHostComparisonsAsync(comparisons, template, query, resolvedPlayer, cancellationToken);
        }

        comparisons = ApplyHammyVizScoring(comparisons);
        notes.Add("These profile metrics are built from live Statsguru result tables and compare the player against the filtered baseline. Own-team style baselines subtract the player's filtered stats before calculating the baseline average.");
        var hammyVizScore = CalculateHammyVizScore(comparisons);
        var hammyVizBlockWeight = CalculateHammyVizBlockWeight(comparisons);

        var response = new PlayerProfileResponse
        {
            RequestedPlayerName = query.PlayerName,
            MatchedPlayerName = resolvedPlayer.DisplayName,
            PlayerId = resolvedPlayer.PlayerId,
            TemplateKey = template.Key,
            TemplateLabel = template.Label,
            Format = template.Format,
            Discipline = template.Discipline,
            TeamId = team?.TeamId,
            TeamName = team?.TeamName,
            ServedFromCache = false,
            CachedAtUtc = null,
            HammyVizScore = hammyVizScore,
            HammyVizBlockWeight = hammyVizBlockWeight,
            HammyVizFormula = BuildHammyVizFormula(comparisons),
            Comparisons = comparisons,
            Notes = notes
        };

        var fetchedUtc = DateTimeOffset.UtcNow;
        var cacheHours = await GetProfileCacheHours(resolvedPlayer.PlayerId, cancellationToken);
        var expiresUtc = fetchedUtc.AddHours(cacheHours);
        var serialized = JsonSerializer.Serialize(response, JsonOptions);
        await _statsCacheRepository.SaveProfileAsync(
            profileCacheKey,
            resolvedPlayer.PlayerId,
            template.Key,
            query.Host,
            query.Opposition,
            query.Team,
            query.Span,
            serialized,
            BuildSourceFingerprint(comparisons),
            fetchedUtc,
            expiresUtc,
            cancellationToken);

        return response;
    }

    public async Task<PlayerCompareResponse> ComparePlayersAsync(StatsQuery playerAQuery, StatsQuery playerBQuery, CancellationToken cancellationToken)
    {
        var playerAProfile = await GetPlayerProfileAsync(playerAQuery, cancellationToken);
        var playerBProfile = await GetPlayerProfileAsync(playerBQuery, cancellationToken);

        var playerBMap = playerBProfile.Comparisons.ToDictionary(comparison => comparison.Key, StringComparer.OrdinalIgnoreCase);
        var rows = new List<PlayerCompareMetricRow>();

        foreach (var playerAComparison in playerAProfile.Comparisons)
        {
            playerBMap.TryGetValue(playerAComparison.Key, out var playerBComparison);

            rows.Add(new PlayerCompareMetricRow
            {
                Key = playerAComparison.Key,
                Label = playerAComparison.Label,
                PlayerAValue = playerAComparison.Metrics.Average,
                PlayerABaseline = playerAComparison.Metrics.BaselineAverage,
                PlayerADelta = playerAComparison.Metrics.AverageDelta,
                PlayerAHammyVizContribution = playerAComparison.Metrics.HammyVizContribution,
                PlayerBValue = playerBComparison?.Metrics.Average,
                PlayerBBaseline = playerBComparison?.Metrics.BaselineAverage,
                PlayerBDelta = playerBComparison?.Metrics.AverageDelta,
                PlayerBHammyVizContribution = playerBComparison?.Metrics.HammyVizContribution
            });
        }

        return new PlayerCompareResponse
        {
            TemplateKey = playerAProfile.TemplateKey,
            TemplateLabel = playerAProfile.TemplateLabel,
            Format = playerAProfile.Format,
            Discipline = playerAProfile.Discipline,
            PlayerA = playerAProfile,
            PlayerB = playerBProfile,
            Rows = rows
        };
    }

    private async Task AddBattingComparisonsAsync(
        List<ProfileComparisonResult> comparisons,
        FilterTemplate template,
        StatsQuery query,
        PlayerLookupResult resolvedPlayer,
        StatsScenarioResult baseScenario,
        int teamId,
        string teamName,
        CancellationToken cancellationToken)
    {
        var top7OwnScenario = await FetchScenarioAsync(
            template,
            query,
            resolvedPlayer.PlayerId,
            [
                new QueryFilter { Key = "team", Value = teamId.ToString() },
                new QueryFilter { Key = "batting_positionval1", Value = "batting_position" },
                new QueryFilter { Key = "batting_positionmin1", Value = "1" },
                new QueryFilter { Key = "batting_positionmax1", Value = "7" }
            ],
            cancellationToken);

        var top7OppScenario = await FetchScenarioAsync(
            template,
            query,
            resolvedPlayer.PlayerId,
            [
                .. BuildOppositionTeamFilters(teamId),
                new QueryFilter { Key = "batting_positionval1", Value = "batting_position" },
                new QueryFilter { Key = "batting_positionmin1", Value = "1" },
                new QueryFilter { Key = "batting_positionmax1", Value = "7" }
            ],
            cancellationToken);

        comparisons.Add(BuildComparison("own-team-top7", $"{teamName} top 7", "Player average versus own-team top-7 batting baseline", template, resolvedPlayer, baseScenario, top7OwnScenario, excludePlayerFromBaseline: true));
        comparisons.Add(BuildComparison("opposition-top7", "Opposition top 7", "Player average versus opposition top-7 batting baseline", template, resolvedPlayer, baseScenario, top7OppScenario, excludePlayerFromBaseline: false));

        if (IsTestTemplate(template))
        {
            await AddHostGroupComparisonsAsync(comparisons, template, query, resolvedPlayer, teamId, teamName, cancellationToken);
        }
    }

    private async Task AddBowlingComparisonsAsync(
        List<ProfileComparisonResult> comparisons,
        FilterTemplate template,
        StatsQuery query,
        PlayerLookupResult resolvedPlayer,
        StatsScenarioResult baseScenario,
        int teamId,
        string teamName,
        CancellationToken cancellationToken)
    {
        var top7Filters = new List<QueryFilter>
        {
            new QueryFilter { Key = "batting_positionval1", Value = "batting_position" },
            new QueryFilter { Key = "batting_positionmin1", Value = "1" },
            new QueryFilter { Key = "batting_positionmax1", Value = "7" }
        };

        if (!string.IsNullOrWhiteSpace(query.BowlerType))
        {
            top7Filters.Add(new QueryFilter { Key = "bowling_pacespin", Value = query.BowlerType });
        }

        var ownTeamTop7Scenario = await FetchScenarioAsync(
            template,
            query,
            resolvedPlayer.PlayerId,
            [
                new QueryFilter { Key = "team", Value = teamId.ToString() },
                .. top7Filters
            ],
            cancellationToken);

        var oppositionTop7Scenario = await FetchScenarioAsync(
            template,
            query,
            resolvedPlayer.PlayerId,
            [
                .. BuildOppositionTeamFilters(teamId),
                .. top7Filters
            ],
            cancellationToken);

        comparisons.Add(BuildComparison("own-team-top7", $"{teamName} vs top 7", "Player bowling average versus own-team top-7 bowling baseline", template, resolvedPlayer, baseScenario, ownTeamTop7Scenario, excludePlayerFromBaseline: true));
        comparisons.Add(BuildComparison("opposition-top7", "Opposition vs top 7", "Player bowling average versus opposition top-7 bowling baseline", template, resolvedPlayer, baseScenario, oppositionTop7Scenario, excludePlayerFromBaseline: false));

        // Add peer discipline comparison (Pacer vs Pacers, Spinner vs Spinners)
        if (!string.IsNullOrWhiteSpace(query.BowlerType))
        {
            var disciplineLabel = query.BowlerType == "1" ? "Pacers" : "Spinners";
            var disciplineKey = query.BowlerType == "1" ? "player-vs-pace" : "player-vs-spin";
            var disciplineFilter = new QueryFilter { Key = "bowling_pacespin", Value = query.BowlerType };
            
            var peerScenario = await FetchScenarioAsync(template, query, resolvedPlayer.PlayerId, [disciplineFilter], cancellationToken);
            comparisons.Add(BuildComparison(disciplineKey, $"vs {disciplineLabel}", $"Player bowling metric versus all {disciplineLabel.ToLowerInvariant()} overall in the match set", template, resolvedPlayer, baseScenario, peerScenario, excludePlayerFromBaseline: true));
        }

        if (!IsTestTemplate(template))
        {
            return;
        }

        await AddHostGroupComparisonsAsync(comparisons, template, query, resolvedPlayer, teamId, teamName, cancellationToken);
    }

    private async Task AddHostGroupComparisonsAsync(
        List<ProfileComparisonResult> comparisons,
        FilterTemplate template,
        StatsQuery query,
        PlayerLookupResult resolvedPlayer,
        int teamId,
        string teamName,
        CancellationToken cancellationToken)
    {
        var groupName = (query.HostGroup ?? "SENA").ToUpperInvariant();
        var hostIds = groupName switch
        {
            "SENAI" => SenaiHostIds,
            "SWENA" => SwenaHostIds,
            _ => SenaHostIds
        };

        var playerGroupScenario = await BuildMergedScenarioAsync(template, query, resolvedPlayer, hostIds, null, cancellationToken);
        var ownTeamGroupScenario = await BuildMergedScenarioAsync(template, query, resolvedPlayer, hostIds, [new QueryFilter { Key = "team", Value = teamId.ToString() }], cancellationToken);
        var oppGroupScenario = await BuildMergedScenarioAsync(template, query, resolvedPlayer, hostIds, BuildOppositionTeamFilters(teamId), cancellationToken);

        comparisons.Add(BuildComparison("player-sena", $"Player in {groupName}", $"Player {template.Discipline.ToLowerInvariant()} in {groupName} host countries versus the {groupName} field excluding the player", template, resolvedPlayer, playerGroupScenario, playerGroupScenario, excludePlayerFromBaseline: true));
        comparisons.Add(BuildComparison("own-team-sena", $"{teamName} in {groupName}", $"Player {template.Discipline.ToLowerInvariant()} in {groupName} versus own-team {groupName} baseline", template, resolvedPlayer, playerGroupScenario, ownTeamGroupScenario, excludePlayerFromBaseline: true));
        comparisons.Add(BuildComparison("opposition-sena", $"Opposition in {groupName}", $"Player {template.Discipline.ToLowerInvariant()} in {groupName} versus opposition {groupName} baseline", template, resolvedPlayer, playerGroupScenario, oppGroupScenario, excludePlayerFromBaseline: false));
    }

    private async Task AddToughestHostComparisonsAsync(
        List<ProfileComparisonResult> comparisons,
        FilterTemplate template,
        StatsQuery query,
        PlayerLookupResult resolvedPlayer,
        CancellationToken cancellationToken)
    {
        var hostGroupFilters =
            new List<QueryFilter>
            {
                new() { Key = "groupby", Value = "host" },
                new() { Key = "orderby", Value = "batting_average" },
                new() { Key = "orderbyad", Value = "reverse" },
                new() { Key = "size", Value = "100" }
            };

        var hostGroupRequest = _urlBuilder.Build(template, query, resolvedPlayer.PlayerId, hostGroupFilters);
        var hostGroupHtml = await _client.GetHtmlAsync(hostGroupRequest.OverallUrl, cancellationToken, query.ForceRefresh);
        var hostIds = ExtractHostIdsFromGroupedHtml(hostGroupHtml)
            .Distinct()
            .Take(5)
            .ToList();

        if (hostIds.Count == 0)
        {
            return;
        }

        var multiHostFilters = hostIds
            .Select(hostId => new QueryFilter { Key = "host", Value = hostId.ToString() })
            .ToList();

        var toughestScenario = await FetchScenarioAsync(template, query, resolvedPlayer.PlayerId, multiHostFilters, cancellationToken);
        var toughestTop7Scenario = await FetchScenarioAsync(
            template,
            query,
            resolvedPlayer.PlayerId,
            [
                .. multiHostFilters,
                new QueryFilter { Key = "batting_positionval1", Value = "batting_position" },
                new QueryFilter { Key = "batting_positionmin1", Value = "1" },
                new QueryFilter { Key = "batting_positionmax1", Value = "7" }
            ],
            cancellationToken);

        comparisons.Add(BuildComparison("player-toughest-hosts", "Player in toughest 5 hosts", "Player average versus the toughest host-country pool excluding the player", template, resolvedPlayer, toughestScenario, toughestScenario, excludePlayerFromBaseline: true));
        comparisons.Add(BuildComparison("toughest-hosts-top7", "Top 7 in toughest hosts", "Player average versus top-7 batting baseline in the toughest host countries", template, resolvedPlayer, toughestScenario, toughestTop7Scenario, excludePlayerFromBaseline: false));
    }

    private FilterTemplate GetTemplate(string templateKey) =>
        _templateRepository.GetByKey(templateKey)
        ?? throw new KeyNotFoundException($"Unknown template key '{templateKey}'.");

    private async Task<(PlayerLookupResult Player, IReadOnlyList<PlayerLookupResult> Candidates, bool PlayerIdFromCache)> ResolvePlayerAsync(
        FilterTemplate template,
        StatsQuery query,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildCacheKey(template, query.PlayerName);
        var cachedPlayer = await _playerLookupCache.TryGetAsync(cacheKey, cancellationToken);

        if (query.SelectedPlayerId is int selectedPlayerId)
        {
            var cachedPlayerRecord = await _statsCacheRepository.TryGetPlayerAsync(selectedPlayerId, cancellationToken);
            if (cachedPlayerRecord is not null)
            {
                var selectedFromCache = new PlayerLookupResult
                {
                    PlayerId = cachedPlayerRecord.PlayerId,
                    DisplayName = cachedPlayerRecord.DisplayName
                };

                await _playerLookupCache.SaveAsync(cacheKey, selectedFromCache, cancellationToken);
                await PersistPlayerRecordAsync(selectedFromCache, cancellationToken);
                return (selectedFromCache, [selectedFromCache], true);
            }

            var selectedPlayer = await ResolveSelectedPlayerAsync(template, query, selectedPlayerId, cancellationToken);
            await _playerLookupCache.SaveAsync(cacheKey, selectedPlayer, cancellationToken);
            await PersistPlayerRecordAsync(selectedPlayer, cancellationToken);
            return (selectedPlayer, [selectedPlayer], false);
        }

        if (cachedPlayer is not null)
        {
            await PersistPlayerRecordAsync(cachedPlayer, cancellationToken);
            return (cachedPlayer, [cachedPlayer], true);
        }

        var searchCandidates = await SearchCandidatesAsync(template, query, cancellationToken);
        var resolvedPlayer = ResolvePlayer(query.PlayerName, searchCandidates);
        await _playerLookupCache.SaveAsync(cacheKey, resolvedPlayer, cancellationToken);
        await PersistPlayerRecordAsync(resolvedPlayer, cancellationToken);
        return (resolvedPlayer, searchCandidates, false);
    }

    private async Task<IReadOnlyList<PlayerLookupResult>> SearchCandidatesAsync(
        FilterTemplate template,
        StatsQuery query,
        CancellationToken cancellationToken)
    {
        var primaryContext = _urlBuilder.Build(template, query);
        var primaryHtml = await _client.GetHtmlAsync(primaryContext.SearchUrl, cancellationToken, query.ForceRefresh);
        var primaryCandidates = _parser.ParsePlayerSearchResults(primaryHtml);
        if (primaryCandidates.Count > 0)
        {
            return primaryCandidates;
        }

        var simplifiedName = query.PlayerName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if (string.IsNullOrWhiteSpace(simplifiedName) ||
            string.Equals(simplifiedName, query.PlayerName, StringComparison.OrdinalIgnoreCase))
        {
            return primaryCandidates;
        }

        var fallbackQuery = new StatsQuery
        {
            PlayerName = simplifiedName,
            TemplateKey = query.TemplateKey,
            ForceRefresh = query.ForceRefresh,
            Host = query.Host,
            Opposition = query.Opposition,
            Team = query.Team,
            Span = query.Span
        };

        var fallbackContext = _urlBuilder.Build(template, fallbackQuery);
        var fallbackHtml = await _client.GetHtmlAsync(fallbackContext.SearchUrl, cancellationToken, query.ForceRefresh);
        return _parser.ParsePlayerSearchResults(fallbackHtml);
    }

    private async Task<PlayerLookupResult> ResolveSelectedPlayerAsync(
        FilterTemplate template,
        StatsQuery query,
        int selectedPlayerId,
        CancellationToken cancellationToken)
    {
        var candidates = await SearchCandidatesAsync(template, query, cancellationToken);
        var selected = candidates.FirstOrDefault(candidate => candidate.PlayerId == selectedPlayerId);

        if (selected is not null)
        {
            return selected;
        }

        if (candidates.Count == 1)
        {
            return candidates[0];
        }

        throw new PlayerSearchMissException(query.PlayerName);
    }

    private async Task<StatsScenarioResult> FetchScenarioAsync(
        FilterTemplate template,
        StatsQuery query,
        int playerId,
        IReadOnlyList<QueryFilter>? extraFilters,
        CancellationToken cancellationToken)
    {
        var requestContext = _urlBuilder.Build(template, query, playerId, extraFilters);
        var overallHtml = await _client.GetHtmlAsync(requestContext.OverallUrl, cancellationToken, query.ForceRefresh);
        var individualHtml = await _client.GetHtmlAsync(requestContext.IndividualUrl, cancellationToken, query.ForceRefresh);

        return new StatsScenarioResult
        {
            SourceLinks = new StatsSourceLinks
            {
                SearchUrl = requestContext.SearchUrl,
                OverallUrl = requestContext.OverallUrl,
                IndividualUrl = requestContext.IndividualUrl
            },
            OverallTable = _parser.Parse(overallHtml),
            IndividualTable = _parser.Parse(individualHtml)
        };
    }

    private async Task<StatsScenarioResult> BuildMergedScenarioAsync(
        FilterTemplate template,
        StatsQuery query,
        PlayerLookupResult resolvedPlayer,
        IEnumerable<int> hostIds,
        IReadOnlyList<QueryFilter>? extraFilters,
        CancellationToken cancellationToken)
    {
        var mergedFilters = hostIds
            .Select(hostId => new QueryFilter { Key = "host", Value = hostId.ToString() })
            .ToList();

        if (extraFilters is not null)
        {
            mergedFilters.AddRange(extraFilters);
        }

        return await FetchScenarioAsync(template, query, resolvedPlayer.PlayerId, mergedFilters, cancellationToken);
    }

    private static string BuildCacheKey(FilterTemplate template, string playerName) =>
        $"{template.Format}:{template.Discipline}:{playerName.Trim().ToLowerInvariant()}";

    private static string BuildProfileCacheKey(FilterTemplate template, StatsQuery query, int playerId) =>
        string.Join(
            '|',
            ProfileCacheVersion,
            template.Key,
            playerId,
            query.Host ?? "-",
            query.Opposition ?? "-",
            query.Team ?? "-",
            query.Span ?? "-",
            query.HostGroup ?? "SENA",
            query.BowlerType ?? "-");

    private async Task PersistPlayerRecordAsync(PlayerLookupResult player, CancellationToken cancellationToken)
    {
        var code = ExtractTeamCode(player.DisplayName);
        var existing = await _statsCacheRepository.TryGetPlayerAsync(player.PlayerId, cancellationToken);

        await _statsCacheRepository.SavePlayerAsync(
            new PlayerCacheRecord
            {
                PlayerId = player.PlayerId,
                DisplayName = player.DisplayName,
                TeamCode = code ?? existing?.TeamCode,
                IsRetired = existing?.IsRetired,
                LastMatchDate = existing?.LastMatchDate,
                TestRuns = existing?.TestRuns,
                OdiRuns = existing?.OdiRuns,
                T20iRuns = existing?.T20iRuns,
                TestWickets = existing?.TestWickets,
                OdiWickets = existing?.OdiWickets,
                T20iWickets = existing?.T20iWickets,
                LastProfileCheckUtc = existing?.LastProfileCheckUtc ?? DateTimeOffset.MinValue,
                UpdatedUtc = DateTimeOffset.UtcNow
            },
            cancellationToken);
    }

    private async Task<int> GetProfileCacheHours(int playerId, CancellationToken cancellationToken)
    {
        var player = await _statsCacheRepository.TryGetPlayerAsync(playerId, cancellationToken);
        return player?.IsRetired == true
            ? _cacheOptions.RetiredProfileCacheHours
            : _cacheOptions.ProfileCacheHours;
    }

    private static string BuildSourceFingerprint(IEnumerable<ProfileComparisonResult> comparisons) =>
        string.Join('|', comparisons.Select(comparison => comparison.Key));

    private static List<QueryFilter> BuildOppositionTeamFilters(int ownTeamId) =>
        TeamResolver.GetOppositionTeamIds(ownTeamId)
            .Select(teamId => new QueryFilter { Key = "team", Value = teamId.ToString() })
            .ToList();

    private static FilterTemplate BuildCompanionTemplate(
        FilterTemplate template,
        string discipline,
        string statView,
        string orderBy) =>
        new()
        {
            Key = $"{template.Key}:{discipline.ToLowerInvariant()}-companion",
            Label = template.Label,
            Format = template.Format,
            Discipline = discipline,
            StatView = statView,
            Summary = template.Summary,
            Metrics = template.Metrics,
            ComparisonGroups = template.ComparisonGroups,
            Notes = template.Notes,
            BaseParameters = template.BaseParameters
                .Where(pair => !string.Equals(pair.Key, "type", StringComparison.OrdinalIgnoreCase) &&
                               !string.Equals(pair.Key, "orderby", StringComparison.OrdinalIgnoreCase))
                .Concat(
                [
                    new KeyValuePair<string, string>("type", statView),
                    new KeyValuePair<string, string>("orderby", orderBy)
                ])
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase)
        };

    private static string? ExtractTeamCode(string displayName)
    {
        var openIndex = displayName.LastIndexOf('(');
        var closeIndex = displayName.LastIndexOf(')');
        if (openIndex < 0 || closeIndex <= openIndex)
        {
            return null;
        }

        return displayName[(openIndex + 1)..closeIndex].Trim();
    }

    private static PlayerLookupResult ResolvePlayer(string requestedName, IReadOnlyList<PlayerLookupResult> candidates)
    {
        if (candidates.Count == 0)
        {
            throw new PlayerSearchMissException(requestedName);
        }

        var exactMatch = candidates.FirstOrDefault(candidate =>
            string.Equals(candidate.DisplayName, requestedName, StringComparison.OrdinalIgnoreCase) ||
            candidate.DisplayName.StartsWith($"{requestedName} ", StringComparison.OrdinalIgnoreCase));

        if (exactMatch is not null)
        {
            return exactMatch;
        }

        if (!requestedName.Contains(' ') && candidates.Count > 1)
        {
            throw new AmbiguousPlayerSearchException(requestedName, candidates.Select(candidate => candidate.DisplayName).ToList());
        }

        return candidates[0];
    }

    private static bool IsBattingTemplate(FilterTemplate template) =>
        string.Equals(template.Discipline, "Batting", StringComparison.OrdinalIgnoreCase);

    private static bool IsTestTemplate(FilterTemplate template) =>
        string.Equals(template.Format, "Test", StringComparison.OrdinalIgnoreCase);

    private static ProfileComparisonResult BuildComparison(
        string key,
        string label,
        string scope,
        FilterTemplate template,
        PlayerLookupResult player,
        StatsScenarioResult subjectScenario,
        StatsScenarioResult baselineScenario,
        string? note = null,
        FilterTemplate? baselineTemplate = null,
        bool excludePlayerFromBaseline = true)
    {
        var subjectRow = GetSubjectRow(template, subjectScenario, player);
        var subjectMetrics = BuildSnapshot(template, subjectRow);
        var effectiveBaselineTemplate = baselineTemplate ?? template;
        var baselineAggregate = GetAggregateRow(effectiveBaselineTemplate, baselineScenario);
        var baselineSubject = GetPlayerSpecificRow(effectiveBaselineTemplate, baselineScenario.IndividualTable, player);
        var baselineRow = excludePlayerFromBaseline
            ? ExcludePlayerFromAggregate(effectiveBaselineTemplate, baselineAggregate, baselineSubject)
            : baselineAggregate;
        var baselineMetrics = BuildSnapshot(effectiveBaselineTemplate, baselineRow);
        var metrics = BuildComparisonSnapshot(template, subjectMetrics, baselineMetrics);

        return new ProfileComparisonResult
        {
            Key = key,
            Label = label,
            Scope = scope,
            SubjectMetrics = subjectMetrics,
            BaselineMetrics = baselineMetrics,
            Metrics = metrics,
            SourceLinks = baselineScenario.SourceLinks,
            Note = note
        };
    }

    private static IReadOnlyDictionary<string, string>? GetAggregateRow(FilterTemplate template, StatsScenarioResult scenario) =>
        scenario.OverallTable.Rows.Count <= 1
            ? scenario.OverallTable.Rows.FirstOrDefault()
            : MergeRows(template, scenario.OverallTable.Rows);

    private static IReadOnlyDictionary<string, string>? FindPlayerRow(StatsTable table, PlayerLookupResult player) =>
        table.Rows.FirstOrDefault(row =>
            row.TryGetValue("Player", out var value) &&
            string.Equals(value, player.DisplayName, StringComparison.OrdinalIgnoreCase));

    private static IReadOnlyDictionary<string, string>? GetPlayerSpecificRow(
        FilterTemplate template,
        StatsTable table,
        PlayerLookupResult player)
    {
        var matchingRows = table.Rows
            .Where(row =>
                (row.TryGetValue("PlayerId", out var idStr) && string.Equals(idStr, player.PlayerId.ToString(), StringComparison.OrdinalIgnoreCase)) ||
                (row.TryGetValue("Player", out var value) && string.Equals(value, player.DisplayName, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (matchingRows.Count == 1)
        {
            return matchingRows[0];
        }

        if (matchingRows.Count > 1)
        {
            return MergeRows(template, matchingRows);
        }

        return table.Rows.Count == 1 ? table.Rows[0] : null;
    }

    private static IReadOnlyDictionary<string, string>? GetSubjectRow(FilterTemplate template, StatsScenarioResult scenario, PlayerLookupResult player)
    {
        return GetPlayerSpecificRow(template, scenario.IndividualTable, player);
    }

    private static IReadOnlyDictionary<string, string>? ExcludePlayerFromAggregate(
        FilterTemplate template,
        IReadOnlyDictionary<string, string>? aggregateRow,
        IReadOnlyDictionary<string, string>? playerRow)
    {
        if (aggregateRow is null)
        {
            return null;
        }

        if (playerRow is null)
        {
            return aggregateRow;
        }

        if (IsBattingTemplate(template))
        {
            var matches = Math.Max(ParseInt(aggregateRow, "Mat") - ParseInt(playerRow, "Mat"), 0);
            var innings = Math.Max(ParseInt(aggregateRow, "Inns") - ParseInt(playerRow, "Inns"), 0);
            var notOuts = Math.Max(ParseInt(aggregateRow, "NO") - ParseInt(playerRow, "NO"), 0);
            var runs = Math.Max(ParseInt(aggregateRow, "Runs") - ParseInt(playerRow, "Runs"), 0);
            var ballsFaced = Math.Max(ParseInt(aggregateRow, "BF") - ParseInt(playerRow, "BF"), 0);

            return new Dictionary<string, string>
            {
                ["Mat"] = matches.ToString(),
                ["Inns"] = innings.ToString(),
                ["NO"] = notOuts.ToString(),
                ["Runs"] = runs.ToString(),
                ["BF"] = ballsFaced.ToString()
            };
        }

        var bowlingMatches = Math.Max(ParseInt(aggregateRow, "Mat") - ParseInt(playerRow, "Mat"), 0);
        var bowlingInnings = Math.Max(ParseInt(aggregateRow, "Inns") - ParseInt(playerRow, "Inns"), 0);
        var runsConceded = Math.Max(ParseInt(aggregateRow, "Runs") - ParseInt(playerRow, "Runs"), 0);
        var wickets = Math.Max(ParseInt(aggregateRow, "Wkts") - ParseInt(playerRow, "Wkts"), 0);
        var remainingBalls = Math.Max(OversToBalls(GetValue(aggregateRow, "Overs")) - OversToBalls(GetValue(playerRow, "Overs")), 0);

        return new Dictionary<string, string>
        {
            ["Mat"] = bowlingMatches.ToString(),
            ["Inns"] = bowlingInnings.ToString(),
            ["Runs"] = runsConceded.ToString(),
            ["Wkts"] = wickets.ToString(),
            ["Overs"] = BallsToOvers(remainingBalls)
        };
    }

    private static ProfileMetricSnapshot BuildComparisonSnapshot(
        FilterTemplate template,
        ProfileMetricSnapshot subjectMetrics,
        ProfileMetricSnapshot baselineMetrics)
    {
        var isBowling = string.Equals(template.Discipline, "Bowling", StringComparison.OrdinalIgnoreCase);
        var averageDelta = isBowling
            ? CalculateDelta(baselineMetrics.Average, subjectMetrics.Average)
            : CalculateDelta(subjectMetrics.Average, baselineMetrics.Average);
        
        var averagePercent = isBowling
            ? CalculateDelta(baselineMetrics.Average, subjectMetrics.Average) / (baselineMetrics.Average != 0 ? baselineMetrics.Average : 1) * 100
            : CalculatePercentDelta(subjectMetrics.Average, baselineMetrics.Average);

        var strikeRateDelta = isBowling
            ? CalculateDelta(baselineMetrics.StrikeRate, subjectMetrics.StrikeRate)
            : CalculateDelta(subjectMetrics.StrikeRate, baselineMetrics.StrikeRate);

        var strikeRatePercent = isBowling
            ? CalculateDelta(baselineMetrics.StrikeRate, subjectMetrics.StrikeRate) / (baselineMetrics.StrikeRate != 0 ? baselineMetrics.StrikeRate : 1) * 100
            : CalculatePercentDelta(subjectMetrics.StrikeRate, baselineMetrics.StrikeRate);

        return new ProfileMetricSnapshot
        {
            Average = subjectMetrics.Average,
            StrikeRate = subjectMetrics.StrikeRate,
            BaselineAverage = baselineMetrics.Average,
            BaselineStrikeRate = baselineMetrics.StrikeRate,
            AverageDelta = averageDelta,
            AverageDeltaPercent = averagePercent,
            StrikeRateDelta = strikeRateDelta,
            StrikeRateDeltaPercent = strikeRatePercent,
            Matches = subjectMetrics.Matches,
            Innings = subjectMetrics.Innings,
            DismissalsOrWickets = subjectMetrics.DismissalsOrWickets,
            RunsOrConceded = subjectMetrics.RunsOrConceded
        };
    }

    private static IReadOnlyDictionary<string, string> MergeRows(FilterTemplate template, IReadOnlyList<IReadOnlyDictionary<string, string>> rows)
    {
        if (rows.Count == 0)
        {
            return new Dictionary<string, string>();
        }

        if (IsBattingTemplate(template))
        {
            var matches = rows.Sum(row => ParseInt(row, "Mat"));
            var innings = rows.Sum(row => ParseInt(row, "Inns"));
            var notOuts = rows.Sum(row => ParseInt(row, "NO"));
            var runs = rows.Sum(row => ParseInt(row, "Runs"));
            var balls = rows.Sum(row => ParseInt(row, "BF"));

            return new Dictionary<string, string>
            {
                ["Mat"] = matches.ToString(),
                ["Inns"] = innings.ToString(),
                ["NO"] = notOuts.ToString(),
                ["Runs"] = runs.ToString(),
                ["BF"] = balls.ToString()
            };
        }

        var bowlingMatches = rows.Sum(row => ParseInt(row, "Mat"));
        var bowlingInnings = rows.Sum(row => ParseInt(row, "Inns"));
        var conceded = rows.Sum(row => ParseInt(row, "Runs"));
        var wickets = rows.Sum(row => ParseInt(row, "Wkts"));
        var bowlingBalls = rows.Sum(row => OversToBalls(GetValue(row, "Overs")));

        return new Dictionary<string, string>
        {
            ["Mat"] = bowlingMatches.ToString(),
            ["Inns"] = bowlingInnings.ToString(),
            ["Runs"] = conceded.ToString(),
            ["Wkts"] = wickets.ToString(),
            ["Overs"] = BallsToOvers(bowlingBalls)
        };
    }

    private static ProfileMetricSnapshot BuildSnapshot(FilterTemplate template, IReadOnlyDictionary<string, string>? row)
    {
        if (row is null || row.Count == 0)
        {
            return new ProfileMetricSnapshot();
        }

        if (IsBattingTemplate(template))
        {
            var matches = ParseNullableInt(row, "Mat");
            var innings = ParseNullableInt(row, "Inns");
            var notOuts = ParseNullableInt(row, "NO") ?? 0;
            var runs = ParseNullableInt(row, "Runs");
            var balls = ParseNullableInt(row, "BF");
            var dismissals = innings.HasValue ? Math.Max(innings.Value - notOuts, 0) : (int?)null;

            return new ProfileMetricSnapshot
            {
                Matches = matches,
                Innings = innings,
                DismissalsOrWickets = dismissals,
                RunsOrConceded = runs,
                Average = runs.HasValue && dismissals is > 0 ? Math.Round((decimal)runs.Value / dismissals.Value, 2) : null,
                StrikeRate = balls.HasValue && balls.Value > 0 && runs.HasValue ? Math.Round((decimal)runs.Value * 100m / balls.Value, 2) : null
            };
        }

        var bowlingMatches = ParseNullableInt(row, "Mat");
        var bowlingInnings = ParseNullableInt(row, "Inns");
        var runsConceded = ParseNullableInt(row, "Runs");
        var wickets = ParseNullableInt(row, "Wkts");
        var ballsBowled = OversToBalls(GetValue(row, "Overs"));

        return new ProfileMetricSnapshot
        {
            Matches = bowlingMatches,
            Innings = bowlingInnings,
            DismissalsOrWickets = wickets,
            RunsOrConceded = runsConceded,
            Average = runsConceded.HasValue && wickets is > 0 ? Math.Round((decimal)runsConceded.Value / wickets.Value, 2) : null,
            StrikeRate = wickets is > 0 && ballsBowled > 0 ? Math.Round((decimal)ballsBowled / wickets.Value, 2) : null
        };
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> row, string key) =>
        row.TryGetValue(key, out var value) ? value : null;

    private static int ParseInt(IReadOnlyDictionary<string, string> row, string key) =>
        ParseNullableInt(row, key) ?? 0;

    private static int? ParseNullableInt(IReadOnlyDictionary<string, string> row, string key)
    {
        if (!row.TryGetValue(key, out var value))
        {
            return null;
        }

        var digitsOnly = new string(value.Where(ch => char.IsDigit(ch) || ch == '-').ToArray());
        return int.TryParse(digitsOnly, out var parsed) ? parsed : null;
    }

    private static int OversToBalls(string? oversValue)
    {
        if (string.IsNullOrWhiteSpace(oversValue))
        {
            return 0;
        }

        var parts = oversValue.Split('.');
        if (!int.TryParse(parts[0], out var overs))
        {
            return 0;
        }

        var balls = 0;
        if (parts.Length > 1)
        {
            int.TryParse(parts[1], out balls);
        }

        return (overs * 6) + balls;
    }

    private static decimal? CalculateDelta(decimal? subject, decimal? baseline) =>
        subject.HasValue && baseline.HasValue
            ? Math.Round(subject.Value - baseline.Value, 2)
            : null;

    private static decimal? CalculatePercentDelta(decimal? subject, decimal? baseline) =>
        subject.HasValue && baseline.HasValue && baseline.Value != 0
            ? Math.Round(((subject.Value - baseline.Value) / baseline.Value) * 100m, 2)
            : null;

    private static List<ProfileComparisonResult> ApplyHammyVizScoring(IEnumerable<ProfileComparisonResult> comparisons)
    {
        var keys =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "all-involved-overall",
                "own-team-overall",
                "opposition-overall",
                "own-team-top7",
                "opposition-top7",
                "player-sena",
                "own-team-sena",
                "opposition-sena",
                "toughest-hosts-top7",
                "player-toughest-hosts",
                "player-vs-pace",
                "player-vs-spin"
            };
        var comparisonList = comparisons.ToList();
        var scoredKeys = comparisonList
            .Where(comparison => keys.Contains(comparison.Key))
            .Where(comparison => comparison.Metrics.AverageDeltaPercent.HasValue)
            .Select(comparison => comparison.Key)
            .ToList();

        if (scoredKeys.Count == 0)
        {
            return comparisonList;
        }

        var blockWeight = Math.Round(100m / scoredKeys.Count, 2);

        return comparisonList
            .Select(comparison =>
            {
                if (!scoredKeys.Contains(comparison.Key, StringComparer.OrdinalIgnoreCase) ||
                    comparison.Metrics.AverageDeltaPercent is null)
                {
                    return comparison;
                }

                var normalized = Math.Clamp((comparison.Metrics.AverageDeltaPercent.Value + 100m) / 200m, 0m, 1m);
                var contribution = Math.Round(blockWeight * normalized, 2);

                return new ProfileComparisonResult
                {
                    Key = comparison.Key,
                    Label = comparison.Label,
                    Scope = comparison.Scope,
                    SubjectMetrics = comparison.SubjectMetrics,
                    BaselineMetrics = comparison.BaselineMetrics,
                    SourceLinks = comparison.SourceLinks,
                    Note = comparison.Note,
                    Metrics = new ProfileMetricSnapshot
                    {
                        Average = comparison.Metrics.Average,
                        StrikeRate = comparison.Metrics.StrikeRate,
                        BaselineAverage = comparison.Metrics.BaselineAverage,
                        BaselineStrikeRate = comparison.Metrics.BaselineStrikeRate,
                        AverageDelta = comparison.Metrics.AverageDelta,
                        AverageDeltaPercent = comparison.Metrics.AverageDeltaPercent,
                        StrikeRateDelta = comparison.Metrics.StrikeRateDelta,
                        StrikeRateDeltaPercent = comparison.Metrics.StrikeRateDeltaPercent,
                        HammyVizContribution = contribution,
                        HammyVizWeightPercent = blockWeight,
                        Matches = comparison.Metrics.Matches,
                        Innings = comparison.Metrics.Innings,
                        DismissalsOrWickets = comparison.Metrics.DismissalsOrWickets,
                        RunsOrConceded = comparison.Metrics.RunsOrConceded
                    }
                };
            })
            .ToList();
    }

    private static decimal? CalculateHammyVizScore(IEnumerable<ProfileComparisonResult> comparisons)
    {
        var contributions = comparisons
            .Select(comparison => comparison.Metrics.HammyVizContribution)
            .Where(value => value.HasValue)
            .Select(value => value!.Value)
            .ToList();

        return contributions.Count == 0
            ? null
            : Math.Round(contributions.Sum(), 2);
    }

    private static decimal? CalculateHammyVizBlockWeight(IEnumerable<ProfileComparisonResult> comparisons)
    {
        var weight = comparisons
            .Select(comparison => comparison.Metrics.HammyVizWeightPercent)
            .FirstOrDefault(value => value is not null);

        return weight;
    }

    private static string? BuildHammyVizFormula(IEnumerable<ProfileComparisonResult> comparisons)
    {
        var weight = CalculateHammyVizBlockWeight(comparisons);
        if (!weight.HasValue)
        {
            return null;
        }

        return $"Each HammyViz block carries {weight.Value:0.##} points out of 100. Block points = weight x clamp((diff% + 100) / 200, 0, 1). Total HammyViz = sum of all block points.";
    }

    private static IEnumerable<int> ExtractHostIdsFromGroupedHtml(string html)
    {
        var matches = System.Text.RegularExpressions.Regex.Matches(
            html,
            @"groupby=host;host=(?<id>\d+);orderby=batting_average",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            if (int.TryParse(match.Groups["id"].Value, out var hostId))
            {
                yield return hostId;
            }
        }
    }

    private static string BallsToOvers(int balls) =>
        $"{balls / 6}.{balls % 6}";
}
