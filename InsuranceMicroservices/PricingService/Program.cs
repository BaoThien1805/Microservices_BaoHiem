
using Steeltoe.Discovery.Client;

var builder = WebApplication.CreateBuilder(args);

// Cấu hình Web API
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Đăng ký Eureka Discovery Client
builder.Services.AddDiscoveryClient();

var app = builder.Build();

// Swagger
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

// API kiểm tra PricingService
app.MapGet("/", () => Results.Ok(new
{
    Service = "PricingService",
    Status = "Running",
    Eureka = "http://localhost:8761"
}));

app.MapGet("/health", () => Results.Ok(new
{
    Status = "Healthy",
    Service = "PricingService"
}));

app.MapControllers();

app.Run();
