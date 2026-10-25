using DashboardService.DataAccess.Elastic;
using DashboardService.Init;
using DashboardService.Messaging.RabbitMq;
using Steeltoe.Discovery.Client;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrEmpty(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://localhost:5035");
builder.Logging.AddJsonConsole();
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(o => o.RegisterServicesFromAssemblyContaining<SalesData>());
builder.Services.AddDiscoveryClient();
builder.Services.AddElasticSearch(builder.Configuration.GetConnectionString("ElasticSearchConnection")
    ?? throw new InvalidOperationException("Missing ElasticSearchConnection."));
if (builder.Configuration.GetValue("RabbitMqOptions:Enabled", true))
    builder.Services.AddRabbitListeners(builder.Configuration);
if (builder.Configuration.GetValue("SeedData:Enabled", false)) builder.Services.AddInitialSalesData();
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { Service = "DashboardService", Status = "Healthy" }));
app.Run();
public partial class Program { }
