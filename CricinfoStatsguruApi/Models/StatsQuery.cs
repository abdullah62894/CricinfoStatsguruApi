namespace CricinfoStatsguruApi.Models;

public sealed class StatsQuery
{
    public required string PlayerName { get; init; }

    public required string TemplateKey { get; init; }

    public int? SelectedPlayerId { get; init; }

    public bool ForceRefresh { get; init; }

    public string? Host { get; init; }

    public string? Opposition { get; init; }

    public string? Team { get; init; }

    public string? Span { get; init; }
    public string? BowlerType { get; init; }
    public string? HostGroup { get; init; }
}
