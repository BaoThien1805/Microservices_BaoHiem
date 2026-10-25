namespace PricingService;

public static class Program
{
    public static Task Main(string[] args) => CreateWebHostBuilder(args).Build().RunAsync();

    public static IHostBuilder CreateWebHostBuilder(string[] args) => Host.CreateDefaultBuilder(args)
        .ConfigureLogging(logging => logging.AddJsonConsole())
        .ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
}
