using PaymentService.Domain;
using PaymentService.Api.Exceptions;
using Xunit;
namespace PaymentService.Test.Domain;

public class InPaymentImportTest : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "th7-import-" + Guid.NewGuid().ToString("N"));
    private readonly DateTimeOffset date = new(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
    public InPaymentImportTest() => Directory.CreateDirectory(directory);
    private string FileName => "bankStatements_2026_11_1.csv";
    private void WriteStatement(string account = "231232132131") => File.WriteAllText(Path.Combine(directory, FileName),
        "id,type,date,account,amount\n1,credit,2026-11-01," + account + ",12.50\n");

    [Fact]
    public async Task ImportsCreditAndDoesNotRepeatProcessedFile()
    {
        WriteStatement();
        var store = new Store();
        await new InPaymentRegistrationService(store).RegisterInPayments(directory, date);
        Assert.Equal(1, store.Commits);
        Assert.True(File.Exists(Path.Combine(directory, "_processed_" + FileName)));
        var account = await store.PolicyAccounts.FindByNumber("POLICY_1");
        Assert.Equal(12.50m, account.BalanceAt(date.AddDays(1)));
        await new InPaymentRegistrationService(store).RegisterInPayments(directory, date);
        Assert.Single(account.Entries);
        Assert.Equal(1, store.Commits);
    }

    [Fact]
    public async Task FailedCommitDoesNotMarkFileProcessed()
    {
        WriteStatement();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new InPaymentRegistrationService(new Store { FailCommit = true }).RegisterInPayments(directory, date));
        Assert.False(File.Exists(Path.Combine(directory, "_processed_" + FileName)));
    }

    [Fact]
    public async Task UnknownAccountDoesNotCommitOrMarkFileProcessed()
    {
        WriteStatement("unknown");
        var store = new Store();
        await Assert.ThrowsAsync<PolicyAccountNotFound>(() =>
            new InPaymentRegistrationService(store).RegisterInPayments(directory, date));
        Assert.Equal(0, store.Commits);
        Assert.False(File.Exists(Path.Combine(directory, "_processed_" + FileName)));
    }

    [Fact]
    public void MalformedStatementHasExplicitError()
    {
        File.WriteAllText(Path.Combine(directory, FileName), "header\nbad-row\n");
        Assert.Throws<FormatException>(() => new BankStatementFile(directory, date).Read());
    }

    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(directory)) File.Delete(file);
        Directory.Delete(directory);
    }

    private sealed class Store : IDataStore
    {
        public IPolicyAccountRepository PolicyAccounts { get; } = new MockPolicyAccountRepository();
        public bool FailCommit { get; init; }
        public int Commits { get; private set; }
        public Task CommitChanges()
        {
            if (FailCommit) throw new InvalidOperationException("Simulated commit failure.");
            Commits++;
            return Task.CompletedTask;
        }
        public void Dispose() { }
    }
}
