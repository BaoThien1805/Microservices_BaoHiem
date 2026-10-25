namespace PricingService.Domain;

public interface IDataStore
{
    ITariffRepository Tariffs { get; }
}

public interface ITariffRepository
{
    Task<Tariff?> this[string code] { get; }
}
