using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using PricingService.DataAccess;
using PricingService.Domain;
using Steeltoe.Discovery.Client;
namespace PricingService;

public sealed class Startup(IConfiguration configuration)
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddControllers().AddNewtonsoftJson();
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen();
        services.AddMediatR(options => options.RegisterServicesFromAssemblyContaining<Startup>());
        services.AddDiscoveryClient(configuration);
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IDataStore, InMemoryDataStore>();
        }
        else
        {
            services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
            services.AddSingleton<PostgresDataStore>();
            services.AddSingleton<IDataStore>(provider => provider.GetRequiredService<PostgresDataStore>());
            services.AddHostedService(provider => provider.GetRequiredService<PostgresDataStore>());
        }
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment environment)
    {
        app.UseExceptionHandler(handler => handler.Run(async context =>
        {
            var exception = context.Features.Get<IExceptionHandlerFeature>()?.Error;
            var status = exception switch
            {
                ValidationException or ArgumentException or ArithmeticException => StatusCodes.Status400BadRequest,
                KeyNotFoundException => StatusCodes.Status404NotFound,
                _ => StatusCodes.Status500InternalServerError
            };
            context.Response.StatusCode = status;
            await context.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = status,
                Title = status switch { 400 => "Invalid pricing request.", 404 => "Product tariff not found.", _ => "Pricing calculation failed." }
            });
        }));
        if (environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
        app.UseRouting();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
            endpoints.MapGet("/", () => Results.Ok(new { Service = "PricingService", Status = "Running", Eureka = "http://localhost:8761" }));
            endpoints.MapGet("/health", () => Results.Ok(new { Service = "PricingService", Status = "Healthy" }));
        });
    }
}
