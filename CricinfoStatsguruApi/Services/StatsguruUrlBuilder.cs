using CricinfoStatsguruApi.Models;
using CricinfoStatsguruApi.Options;
using Microsoft.Extensions.Options;

namespace CricinfoStatsguruApi.Services;

public sealed class StatsguruUrlBuilder(IOptions<StatsguruOptions> options) : IStatsguruUrlBuilder
{
    private readonly StatsguruOptions _options = options.Value;

    public StatsguruRequestContext Build(
        FilterTemplate template,
        StatsQuery query,
        int? playerId = null,
        IReadOnlyList<QueryFilter>? extraFilters = null)
    {
        var baseParameters = template.BaseParameters
            .Select(pair => new QueryFilter { Key = pair.Key, Value = pair.Value })
            .ToList();

        AddOrReplace(baseParameters, "host", query.Host);
        AddOrReplace(baseParameters, "opposition", query.Opposition);
        AddOrReplace(baseParameters, "team", query.Team);
        AddOrReplace(baseParameters, "bowling_pacespin", query.BowlerType);

        if (!string.IsNullOrWhiteSpace(query.Span))
        {
            AddOrReplace(baseParameters, "spanval1", "span");
            AddOrReplace(baseParameters, "spanmin1", query.Span);
        }

        if (extraFilters is not null)
        {
            foreach (var pair in extraFilters)
            {
                if (IsRepeatableFilter(pair.Key))
                {
                    baseParameters.Add(new QueryFilter { Key = pair.Key, Value = pair.Value });
                }
                else
                {
                    AddOrReplace(baseParameters, pair.Key, pair.Value);
                }
            }
        }

        var searchParameters = Clone(baseParameters);
        if (!string.IsNullOrWhiteSpace(query.PlayerName))
        {
            AddOrReplace(searchParameters, "search_player", query.PlayerName);
        }

        RemoveAll(searchParameters, "template");
        RemoveAll(searchParameters, "groupby");
        RemoveAll(searchParameters, "player_involve");

        List<QueryFilter> overallParameters;
        List<QueryFilter> individualParameters;

        if (playerId is int resolvedPlayerId)
        {
            overallParameters = Clone(baseParameters);
            AddOrReplace(overallParameters, "player_involve", resolvedPlayerId.ToString());
            if (!overallParameters.Any(filter => string.Equals(filter.Key, "groupby", StringComparison.OrdinalIgnoreCase)))
            {
                AddOrReplace(overallParameters, "groupby", "overall");
            }

            individualParameters = Clone(baseParameters);
            AddOrReplace(individualParameters, "player_involve", resolvedPlayerId.ToString());
        }
        else
        {
            overallParameters = Clone(baseParameters);
            individualParameters = Clone(baseParameters);
        }

        return new StatsguruRequestContext
        {
            AppliedFilters = baseParameters,
            SearchUrl = BuildUrl(searchParameters),
            OverallUrl = BuildUrl(overallParameters),
            IndividualUrl = BuildUrl(individualParameters)
        };
    }

    private string BuildUrl(IReadOnlyList<QueryFilter> parameters)
    {
        var queryString = string.Join(
            ';',
            parameters.Select(pair => $"{pair.Key}={Uri.EscapeDataString(pair.Value)}"));

        return $"{_options.BaseUrl}?{queryString}";
    }

    private static void AddOrReplace(List<QueryFilter> filters, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            RemoveAll(filters, key);
            return;
        }

        RemoveAll(filters, key);
        filters.Add(new QueryFilter { Key = key, Value = value });
    }

    private static void RemoveAll(List<QueryFilter> filters, string key) =>
        filters.RemoveAll(filter => string.Equals(filter.Key, key, StringComparison.OrdinalIgnoreCase));

    private static List<QueryFilter> Clone(IEnumerable<QueryFilter> filters) =>
        filters.Select(filter => new QueryFilter { Key = filter.Key, Value = filter.Value }).ToList();

    private static bool IsRepeatableFilter(string key) =>
        key is "host" or "opposition" or "team";
}
