using EasyNetQ;
namespace DashboardService.Messaging.RabbitMq;
public static class RabbitInstaller
{
    public static IServiceCollection AddRabbitListeners(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IBus>(_ =>
        {
            var options = configuration.GetSection("RabbitMqOptions");
            return RabbitHutch.CreateBus(new ConnectionConfiguration
            {
                Hosts = new List<HostConfiguration> { new() { Host = options["Host"] ?? "localhost", Port = options.GetValue<ushort>("Port", 5672) } },
                UserName = options["Username"] ?? "guest",
                Password = options["Password"] ?? "guest"
            }, _ => { });
        });
        services.AddHostedService<RabbitEventListener>();
        return services;
    }
}
