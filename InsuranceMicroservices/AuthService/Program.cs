using System.Text;
using AuthService;
using AuthService.DataAccess;
using AuthService.Domain;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Steeltoe.Discovery.Client;

var builder = WebApplication.CreateBuilder(args);
if (string.IsNullOrEmpty(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://localhost:6060");
builder.Logging.AddJsonConsole();
var settings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>() ?? new();
if (Encoding.UTF8.GetByteCount(settings.Secret) < 32)
    throw new InvalidOperationException("Set AppSettings__Secret to a shared random key of at least 32 bytes.");
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.WithOrigins(settings.AllowedAuthOrigins).AllowAnyHeader().AllowAnyMethod()));
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
        ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
        ClockSkew = TimeSpan.FromSeconds(30)
    };
});
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IInsuranceAgents, InsuranceAgentsInMemoryDb>();
builder.Services.AddSingleton<AuthService.Domain.AuthService>();
builder.Services.AddDiscoveryClient();
var app = builder.Build();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { Service = "AuthService", Status = "Healthy" }));
app.Run();
public partial class Program { }
