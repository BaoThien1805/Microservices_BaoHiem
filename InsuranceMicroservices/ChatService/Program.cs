using ChatService;
using ChatService.Hubs;
using ChatService.Messaging.RabbitMq;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.SignalR;
using Microsoft.IdentityModel.Tokens;
using Steeltoe.Discovery.Client;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrEmpty(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://localhost:4099");
builder.Logging.AddJsonConsole();
var settings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new();
if (Encoding.UTF8.GetByteCount(settings.Secret) < 32)
    throw new InvalidOperationException("Set AppSettings__Secret to a shared random key of at least 32 bytes.");
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMediatR(o => o.RegisterServicesFromAssemblyContaining<AgentChatHub>());
builder.Services.AddSignalR();
builder.Services.AddSingleton<IUserIdProvider, NameUserIdProvider>();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(settings.AllowedChatOrigins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
        ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
    o.Events = new JwtBearerEvents { OnMessageReceived = context =>
    {
        if (context.Request.Path.StartsWithSegments("/agentsChat") && context.Request.Query.TryGetValue("access_token", out var token))
            context.Token = token;
        return Task.CompletedTask;
    }};
});
builder.Services.AddAuthorization();
builder.Services.AddDiscoveryClient();
if (builder.Configuration.GetValue("RabbitMqOptions:Enabled", true))
    builder.Services.AddRabbitListeners(builder.Configuration);
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<AgentChatHub>("/agentsChat");
app.MapGet("/health", () => Results.Ok(new { Service = "ChatService", Status = "Healthy" }));
app.Run();
public partial class Program { }
