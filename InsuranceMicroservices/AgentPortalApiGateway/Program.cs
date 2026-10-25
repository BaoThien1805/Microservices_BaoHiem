using System.Text;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Ocelot.Cache.CacheManager;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Eureka;

namespace AgentPortalApiGateway;

public class Program
{
    public static void Main(string[] args)
    {
        BuildWebHost(args).Run();
    }

    public static IWebHost BuildWebHost(string[] args)
    {
        return WebHost.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((hostingContext, config) =>
            {
                config
                    .SetBasePath(hostingContext.HostingEnvironment.ContentRootPath)
                    .AddJsonFile("appsettings.json", true, true)
                    .AddJsonFile($"appsettings.{hostingContext.HostingEnvironment.EnvironmentName}.json", true,
                        true)
                    .AddJsonFile("ocelot.json", false, false)
                    .AddJsonFile($"ocelot.{hostingContext.HostingEnvironment.EnvironmentName}.json", true, true)
                    .AddEnvironmentVariables();
            })
            .UseUrls(System.Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? "http://localhost:8099")
            .ConfigureServices((context, s) =>
            {
                var secret = context.Configuration["AppSettings:Secret"] ?? "";
                if (Encoding.UTF8.GetByteCount(secret) < 32)
                    throw new System.InvalidOperationException("Set AppSettings__Secret to a shared random key of at least 32 bytes.");
                var key = Encoding.UTF8.GetBytes(secret);
                s.AddCors();
                s.AddAuthentication(x =>
                    {
                        x.DefaultAuthenticateScheme = "ApiSecurity";
                        x.DefaultChallengeScheme = "ApiSecurity";
                    })
                    .AddJwtBearer("ApiSecurity", x =>
                    {
                        x.RequireHttpsMetadata = false;
                        x.SaveToken = true;
                        x.TokenValidationParameters = new TokenValidationParameters
                        {
                            ValidateIssuerSigningKey = true,
                            IssuerSigningKey = new SymmetricSecurityKey(key),
                            ValidateIssuer = false,
                            ValidateAudience = false
                        };
                    });
                s.AddOcelot().AddEureka().AddCacheManager(x => x.WithDictionaryHandle());
            })
            .Configure(a =>
            {
                var appSettings = new AppSettings();
                a.ApplicationServices.GetService<IConfiguration>()
                    .GetSection("AppSettings")
                    .Bind(appSettings);

                a.UseCors
                (b => b
                    .WithOrigins(appSettings.AllowedChatOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials()
                );
                a.UseOcelot().Wait();
            })
            .Build();
    }
}