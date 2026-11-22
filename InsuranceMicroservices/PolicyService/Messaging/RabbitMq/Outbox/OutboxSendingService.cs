namespace PolicyService.Messaging.RabbitMq.Outbox;

public sealed class OutboxSendingService(Outbox outbox, ILogger<OutboxSendingService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try { await outbox.PushPendingMessages(); }
            catch (Exception error) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(error, "Policy outbox processing failed; messages remain pending");
            }
        }
    }
}
