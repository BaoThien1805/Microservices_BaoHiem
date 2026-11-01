namespace PaymentService.Init;
public sealed class PaymentInitializer(IServiceProvider services, IConfiguration configuration) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("SeedData:Enabled", true)) return;
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DataLoader>().Seed();
    }
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
