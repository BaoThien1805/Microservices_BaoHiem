using Microsoft.AspNetCore;

namespace PolicyService;

public static class Program
{
    public static void Main(string[] args) => CreateWebHostBuilder(args).Build().Run();

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5050")
            .ConfigureLogging(logging => { logging.ClearProviders(); logging.AddJsonConsole(); })
            .UseStartup<Startup>();
}
