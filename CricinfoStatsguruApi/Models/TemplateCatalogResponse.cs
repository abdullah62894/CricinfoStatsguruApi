namespace CricinfoStatsguruApi.Models;

public sealed class TemplateCatalogResponse
{
    public required int Count { get; init; }

    public required IReadOnlyList<FilterTemplate> Templates { get; init; }
}
