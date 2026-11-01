using System;
using System.Collections.Generic;
using GlobalExceptionHandler.WebApi;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using PaymentService.Configuration;
using PaymentService.DataAccess.Marten;
using PaymentService.Domain;
using PaymentService.Infrastructure;
using PaymentService.Init;
using PaymentService.Jobs;
using PaymentService.Messaging.RabbitMq;
using PolicyService.Api.Events;
using Steeltoe.Discovery.Client;

namespace PaymentService;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    // This method gets called by the runtime. Use this method to add services to the container.
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddMvc()
            .AddNewtonsoftJson();

        services.AddMarten(Configuration.GetConnectionString("PgConnection"));
        services.AddPaymentDemoInitializer();
        services.AddHostedService<PaymentInitializer>();
        services.AddMediatR(opts => opts.RegisterServicesFromAssemblyContaining<Startup>());
        services.AddLogingBehaviour();
        services.AddSingleton<PolicyAccountNumberGenerator>();
        services.AddDiscoveryClient(Configuration);
        if (Configuration.GetValue("RabbitMqOptions:Enabled", true))
            services.AddRabbitListeners(Configuration);
        if (Configuration.GetValue("BackgroundJobs:Enabled", true))
            services.AddBackgroundJobs(Configuration.GetSection("BackgroundJobs").Get<BackgroundJobsConfig>());
        services.AddSwaggerGen();
    }

    // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        app.UseRouting();
        app.UseGlobalExceptionHandler(cfg => cfg.MapExceptions());
        if (!env.IsDevelopment()) app.UseHsts();

        if (env.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        if (Configuration.GetValue("BackgroundJobs:Enabled", true)) app.UseBackgroundJobs();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}