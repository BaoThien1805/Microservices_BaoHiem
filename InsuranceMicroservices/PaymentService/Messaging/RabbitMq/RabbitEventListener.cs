using EasyNetQ;
using EasyNetQ.Topology;
using MediatR;
using PolicyService.Api.Events;
namespace PaymentService.Messaging.RabbitMq;

public sealed class RabbitEventListener(IBus bus, IServiceProvider services) : IHostedService, IDisposable
{
    private readonly List<IDisposable> consumers = new();
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Subscribe<PolicyCreated>(cancellationToken);
        await Subscribe<PolicyTerminated>(cancellationToken);
    }
    private async Task Subscribe<T>(CancellationToken cancellationToken) where T : class, INotification
    {
        var exchange = await bus.Advanced.ExchangeDeclareAsync("lab-dotnet-micro", ExchangeType.Topic, cancellationToken: cancellationToken);
        var queue = await bus.Advanced.QueueDeclareAsync("lab-payment-service-" + typeof(T).Name, cancellationToken: cancellationToken);
        await bus.Advanced.BindAsync(exchange, queue, typeof(T).Name.ToLowerInvariant(), cancellationToken: cancellationToken);
        consumers.Add(bus.Advanced.Consume<T>(queue, async (message, _) =>
        {
            using var scope = services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(message.Body);
        }));
    }
    public Task StopAsync(CancellationToken cancellationToken) { Dispose(); return Task.CompletedTask; }
    public void Dispose() { foreach (var consumer in consumers) consumer.Dispose(); consumers.Clear(); }
}
