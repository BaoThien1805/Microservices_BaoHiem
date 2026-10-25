using EasyNetQ;
using EasyNetQ.Topology;
using MediatR;
using PolicyService.Api.Events;
namespace ChatService.Messaging.RabbitMq;

public sealed class RabbitEventListener(IBus bus, IServiceProvider services) : IHostedService, IDisposable
{
    private IDisposable? consumer;
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var exchange = await bus.Advanced.ExchangeDeclareAsync("lab-dotnet-micro", ExchangeType.Topic, cancellationToken: cancellationToken);
        var queue = await bus.Advanced.QueueDeclareAsync("lab-chat-service-PolicyCreated", cancellationToken: cancellationToken);
        await bus.Advanced.BindAsync(exchange, queue, "policycreated", cancellationToken: cancellationToken);
        consumer = bus.Advanced.Consume<PolicyCreated>(queue, async (message, _) =>
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(message.Body);
        });
    }
    public Task StopAsync(CancellationToken cancellationToken) { Dispose(); return Task.CompletedTask; }
    public void Dispose() { consumer?.Dispose(); consumer = null; }
}
