using System.Net;
using System.Text.RegularExpressions;
using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public sealed partial class StatsguruHtmlParser : IStatsguruHtmlParser
{
    public IReadOnlyList<PlayerLookupResult> ParsePlayerSearchResults(string html)
    {
        var normalizedHtml = Regex.Replace(html, "\\s+", " ");
        var selectMatch = PlayerInvolveSelectRegex().Match(normalizedHtml);
        if (!selectMatch.Success)
        {
            var singleMatch = SinglePlayerInvolveRegex().Match(normalizedHtml);
            if (!singleMatch.Success)
            {
                return Array.Empty<PlayerLookupResult>();
            }

            var singleValue = singleMatch.Groups["value"].Value.Trim();
            if (!int.TryParse(singleValue, out var singlePlayerId))
            {
                return Array.Empty<PlayerLookupResult>();
            }

            var rawLabel = WebUtility.HtmlDecode(singleMatch.Groups["label"].Value).Trim();
            var displayName = NormalizeSinglePlayerLabel(rawLabel);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return Array.Empty<PlayerLookupResult>();
            }

            return
            [
                new PlayerLookupResult
                {
                    PlayerId = singlePlayerId,
                    DisplayName = displayName
                }
            ];
        }

        var results = new List<PlayerLookupResult>();
        foreach (Match optionMatch in PlayerOptionRegex().Matches(selectMatch.Groups["options"].Value))
        {
            var value = optionMatch.Groups["value"].Value.Trim();
            if (!int.TryParse(value, out var playerId))
            {
                continue;
            }

            var displayName = WebUtility.HtmlDecode(optionMatch.Groups["label"].Value).Trim();
            if (string.IsNullOrWhiteSpace(displayName))
            {
                continue;
            }

            results.Add(new PlayerLookupResult
            {
                PlayerId = playerId,
                DisplayName = displayName
            });
        }

        return results;
    }

    private static string NormalizeSinglePlayerLabel(string rawLabel)
    {
        var cleaned = HtmlTagRegex().Replace(rawLabel, " ");
        cleaned = Regex.Replace(cleaned, "\\s+", " ").Trim();

        var colonIndex = cleaned.LastIndexOf(':');
        if (colonIndex >= 0 && colonIndex < cleaned.Length - 1)
        {
            cleaned = cleaned[(colonIndex + 1)..].Trim();
        }

        return cleaned;
    }

    public StatsTable Parse(string html)
    {
        var normalizedHtml = Regex.Replace(html, "\\s+", " ");
        var tableMatches = TableRegex().Matches(normalizedHtml);
        StatsTable? bestTable = null;
        var bestScore = -1;

        foreach (Match tableMatch in tableMatches)
        {
            var attributes = tableMatch.Groups["attrs"].Value;
            var tableHtml = tableMatch.Groups["table"].Value;
            var headers = ExtractCells(tableHtml, "th");
            var rowMatches = TableRowRegex().Matches(tableHtml);
            var rows = new List<IReadOnlyDictionary<string, string>>();

            foreach (Match rowMatch in rowMatches)
            {
                var rowHtml = rowMatch.Groups["row"].Value;
                var cells = ExtractCells(rowHtml, "td");
                var rawCells = ExtractRawCells(rowHtml, "td");
                if (cells.Count == 0)
                {
                    continue;
                }

                var effectiveHeaders = BuildEffectiveHeaders(headers, cells.Count);

                if (effectiveHeaders.Count == 0)
                {
                    effectiveHeaders = Enumerable.Range(1, cells.Count).Select(index => $"Column {index}").ToList();
                }

                var row = effectiveHeaders
                    .Zip(cells, (header, value) => new KeyValuePair<string, string>(header, value))
                    .ToDictionary();

                var playerColumnIndex = effectiveHeaders.FindIndex(header =>
                    string.Equals(header, "Player", StringComparison.OrdinalIgnoreCase));

                if (playerColumnIndex >= 0 && playerColumnIndex < rawCells.Count)
                {
                    var playerId = ExtractPlayerId(rawCells[playerColumnIndex]);
                    if (playerId.HasValue)
                    {
                        row["PlayerId"] = playerId.Value.ToString();
                    }
                }

                rows.Add(row);
            }

            if (rows.Count > 0)
            {
                var effectiveHeaders = BuildEffectiveHeaders(headers, rows.First().Count);

                var candidate = new StatsTable
                {
                    Headers = effectiveHeaders,
                    Rows = rows
                };

                var score = ScoreCandidate(attributes, candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTable = candidate;
                }
            }
        }

        if (bestTable is not null)
        {
            return bestTable;
        }

        return new StatsTable
        {
            Headers = Array.Empty<string>(),
            Rows = Array.Empty<IReadOnlyDictionary<string, string>>()
        };
    }

    private static int ScoreCandidate(string attributes, StatsTable table)
    {
        var headerScore = table.Headers.Count * 10;
        var rowScore = table.Rows.Count * Math.Max(table.Headers.Count, 1);
        var engineTableBonus = attributes.Contains("engineTable", StringComparison.OrdinalIgnoreCase) ? 500 : 0;
        var meaningfulHeaderBonus = table.Headers.Any(header =>
            header is "Mat" or "Inns" or "Runs" or "Ave" or "SR" or "Wkts" or "Bowl Ave" or "Span")
            ? 500
            : 0;
        var playerHeaderBonus = table.Headers.Any(header =>
            string.Equals(header, "Player", StringComparison.OrdinalIgnoreCase))
            ? 1200
            : 0;
        var playerIdBonus = table.Rows.Any(row => row.ContainsKey("PlayerId")) ? 2000 : 0;
        var paginationPenalty = table.Rows.Any(row =>
            row.Values.Any(value => value.Contains("Page 1 of", StringComparison.OrdinalIgnoreCase)) &&
            row.Values.Any(value => value.Contains("Showing", StringComparison.OrdinalIgnoreCase)))
            ? -1800
            : 0;

        return engineTableBonus + meaningfulHeaderBonus + playerHeaderBonus + playerIdBonus + paginationPenalty + headerScore + rowScore;
    }

    private static List<string> BuildEffectiveHeaders(List<string> headers, int cellCount)
    {
        if (headers.Count >= cellCount)
        {
            return headers.Take(cellCount).ToList();
        }

        if (headers.Count == cellCount - 1)
        {
            return ["Label", .. headers];
        }

        return Enumerable.Range(1, cellCount).Select(index => $"Column {index}").ToList();
    }

    private static List<string> ExtractCells(string html, string cellTag)
    {
        var regex = new Regex($@"<{cellTag}\b[^>]*>(?<value>.*?)</{cellTag}>", RegexOptions.IgnoreCase);
        var results = new List<string>();

        foreach (Match match in regex.Matches(html))
        {
            var withoutTags = HtmlTagRegex().Replace(match.Groups["value"].Value, " ");
            var decoded = WebUtility.HtmlDecode(withoutTags);
            var cleaned = Regex.Replace(decoded, "\\s+", " ").Trim();

            if (!string.IsNullOrWhiteSpace(cleaned))
            {
                results.Add(cleaned);
            }
        }

        return results;
    }

    private static List<string> ExtractRawCells(string html, string cellTag)
    {
        var regex = new Regex($@"<{cellTag}\b[^>]*>(?<value>.*?)</{cellTag}>", RegexOptions.IgnoreCase);
        return regex.Matches(html)
            .Select(match => match.Groups["value"].Value)
            .ToList();
    }

    private static int? ExtractPlayerId(string rawCellHtml)
    {
        var match = PlayerLinkRegex().Match(rawCellHtml);
        if (!match.Success)
        {
            return null;
        }

        var value = match.Groups["id"].Success
            ? match.Groups["id"].Value
            : match.Groups["id2"].Value;

        return int.TryParse(value, out var playerId) ? playerId : null;
    }

    [GeneratedRegex("<table\\b(?<attrs>[^>]*)>(?<table>.*?)</table>", RegexOptions.IgnoreCase)]
    private static partial Regex TableRegex();

    [GeneratedRegex("<tr\\b[^>]*>(?<row>.*?)</tr>", RegexOptions.IgnoreCase)]
    private static partial Regex TableRowRegex();

    [GeneratedRegex("<.*?>", RegexOptions.IgnoreCase)]
    private static partial Regex HtmlTagRegex();

    [GeneratedRegex("<select\\b[^>]*name=\"player_involve\"[^>]*>(?<options>.*?)</select>", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerInvolveSelectRegex();

    [GeneratedRegex("<option\\b[^>]*value=\"(?<value>[^\"]*)\"[^>]*>(?<label>.*?)</option>", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerOptionRegex();

    [GeneratedRegex("<input\\b[^>]*name=\"player_involve\"[^>]*value=\"(?<value>\\d+)\"[^>]*>\\s*(?<label>[^<]+)", RegexOptions.IgnoreCase)]
    private static partial Regex SinglePlayerInvolveRegex();

    [GeneratedRegex("(?:/player/(?<id>\\d+)\\.html|/cricketers/[^\"'<>]*-(?<id2>\\d+))", RegexOptions.IgnoreCase)]
    private static partial Regex PlayerLinkRegex();
}
