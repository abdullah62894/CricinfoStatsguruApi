namespace CricinfoStatsguruApi.Models;

public sealed class FilterTemplate
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Format { get; init; }

    public required string Discipline { get; init; }

    public required string StatView { get; init; }

    public required string Summary { get; init; }

    public required IReadOnlyList<string> Metrics { get; init; }

    public required IReadOnlyList<string> ComparisonGroups { get; init; }

    public required IReadOnlyList<string> Notes { get; init; }

    public required IReadOnlyDictionary<string, string> BaseParameters { get; init; }
}
