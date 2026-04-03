using CricinfoStatsguruApi.Models;

namespace CricinfoStatsguruApi.Services;

public interface IFilterTemplateRepository
{
    IReadOnlyList<FilterTemplate> GetAll();

    FilterTemplate? GetByKey(string key);
}
