using Microsoft.AspNetCore;

namespace PolicySearchService;

public static class Program
{
    public static void Main(string[] args) => CreateWebHostBuilder(args).Build().Run();

    public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
        WebHost.CreateDefaultBuilder(args)
            .UseUrls(Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:5065")
            .ConfigureLogging(logging => { logging.ClearProviders(); logging.AddJsonConsole(); })
            .UseStartup<Startup>();
}
