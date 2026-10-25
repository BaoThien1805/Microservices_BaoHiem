using System.Text.Json;
using Npgsql;
using NpgsqlTypes;
using PricingService.Domain;
namespace PricingService.DataAccess;

public sealed class PostgresDataStore(NpgsqlDataSource dataSource) : IDataStore, ITariffRepository, IHostedService
{
    public ITariffRepository Tariffs => this;
    public Task<Tariff?> this[string code] => FindAsync(code);

    private async Task<Tariff?> FindAsync(string code)
    {
        await using var command = dataSource.CreateCommand("SELECT definition FROM pricing_tariffs WHERE code = $1");
        command.Parameters.AddWithValue(code);
        var json = await command.ExecuteScalarAsync() as string;
        if (json is null) return null;
        var definition = JsonSerializer.Deserialize<TariffDefinition>(json)
            ?? throw new InvalidOperationException("Stored tariff definition is invalid.");
        return definition.ToTariff(code);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using (var create = new NpgsqlCommand("""
            CREATE TABLE IF NOT EXISTS pricing_tariffs (
                code text PRIMARY KEY,
                definition jsonb NOT NULL
            )
            """, connection, transaction))
            await create.ExecuteNonQueryAsync(cancellationToken);
        foreach (var tariff in DefaultTariffs.All())
        {
            await using var insert = new NpgsqlCommand("""
                INSERT INTO pricing_tariffs (code, definition) VALUES ($1, $2)
                ON CONFLICT (code) DO NOTHING
                """, connection, transaction);
            insert.Parameters.AddWithValue(tariff.Code);
            insert.Parameters.AddWithValue(NpgsqlDbType.Jsonb, JsonSerializer.Serialize(
                new TariffDefinition(tariff.BasePremiumRules.Rules.ToArray(), tariff.DiscountMarkupRules.Rules.ToArray())));
            await insert.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private sealed record TariffDefinition(BasePremiumRule[] BaseRules, DiscountMarkupRule[] MarkupRules)
    {
        public Tariff ToTariff(string code)
        {
            var tariff = new Tariff(code);
            foreach (var rule in BaseRules) tariff.BasePremiumRules.AddBasePriceRule(rule.CoverCode, rule.Condition, rule.Expression);
            foreach (var rule in MarkupRules) tariff.DiscountMarkupRules.AddPercentMarkup(rule.Condition, rule.Multiplier);
            return tariff;
        }
    }
}
