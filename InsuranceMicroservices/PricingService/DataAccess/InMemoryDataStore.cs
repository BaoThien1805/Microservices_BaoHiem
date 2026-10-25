using PricingService.Domain;
namespace PricingService.DataAccess;

public sealed class InMemoryDataStore : IDataStore, ITariffRepository
{
    private readonly IReadOnlyDictionary<string, Tariff> tariffs = DefaultTariffs.All().ToDictionary(tariff => tariff.Code, StringComparer.Ordinal);
    public ITariffRepository Tariffs => this;
    public Task<Tariff?> this[string code] => Task.FromResult(tariffs.GetValueOrDefault(code));
}
