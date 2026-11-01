using Microsoft.Extensions.DependencyInjection;
using PaymentService.DataAccess.Marten;
using PaymentService.Domain;
using Testcontainers.PostgreSql;
using Xunit;
namespace PaymentService.Test;

public class MartenPersistenceTest
{
    [PostgresFact]
    public async Task AccountEntriesAndProtectedSettersSurviveRoundTrip()
    {
        await using var postgres = new PostgreSqlBuilder().WithImage("postgres:16-alpine").Build();
        await postgres.StartAsync();
        var services = new ServiceCollection();
        MartenInstaller.AddMarten(services, postgres.GetConnectionString());
        using var provider = services.BuildServiceProvider();
        var date = DateTimeOffset.UtcNow;
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDataStore>();
            var account = new PolicyAccount("POLICY_TEST", "ACCOUNT_TEST", "Test", "Owner");
            account.ExpectedPayment(100m, date);
            account.InPayment(70m, date);
            store.PolicyAccounts.Add(account);
            await store.CommitChanges();
        }
        using (var scope = provider.CreateScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IDataStore>();
            var byPolicy = await store.PolicyAccounts.FindByNumber("POLICY_TEST");
            var byAccount = await store.PolicyAccounts.FindByAccountNumber("ACCOUNT_TEST");
            Assert.Equal(byPolicy.Id, byAccount.Id);
            Assert.Equal("Test", byPolicy.Owner.FirstName);
            Assert.Equal(-30m, byPolicy.BalanceAt(date));
            Assert.Contains(byPolicy.Entries, e => e is ExpectedPayment);
            Assert.Contains(byPolicy.Entries, e => e is InPayment);
        }
    }
}
public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_DOCKER_TESTS") != "1")
            Skip = "Set RUN_DOCKER_TESTS=1 with Docker running for the Marten/PostgreSQL round-trip test.";
    }
}
