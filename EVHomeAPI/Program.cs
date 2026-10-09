/*
 * EVHomeAPI — Nabla Home backend (home chargers, no payments).
 *
 * Users + JWT auth, stations + claim-by-code, charging sessions driven by EVOCPP
 * events (RabbitMQ), RemoteStart/Stop commands, SignalR hub for live updates.
 */

using System.Text;
using System.Threading.RateLimiting;
using EVHomeAPI.Data;
using EVHomeAPI.Hubs;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config  = builder.Configuration;

// ── Fail fast on unsafe production config ─────────────────────────────────────
if (builder.Environment.IsProduction())
{
    var jwtKey = config["Jwt:Key"] ?? "";
    if (jwtKey.Length < 32 || jwtKey.StartsWith("dev-only", StringComparison.Ordinal))
        throw new InvalidOperationException("Jwt:Key must be set to a random secret of at least 32 characters in Production.");
    if ((config["AdminKey"] ?? "").Length < 24)
        throw new InvalidOperationException("AdminKey must be set to a random secret of at least 24 characters in Production.");
}

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<ChargingLimitService>();
builder.Services.AddSingleton(TimeProvider.System);

// ── EVOCPP integration (RabbitMQ) ─────────────────────────────────────────────
builder.Services.AddSingleton(config.GetSection(RabbitMqOptions.Section).Get<RabbitMqOptions>() ?? new RabbitMqOptions());
builder.Services.AddSingleton<RabbitMqConnection>();
builder.Services.AddSingleton<IOcppCommandPublisher, RabbitMqCommandPublisher>();
builder.Services.AddSingleton<INotifier, SignalRNotifier>();
builder.Services.AddScoped<OcppEventProcessor>();

// ── Developer tooling (/api/stations/{id}/dev/*, off unless DevTools:Enabled) ──
var devTools = config.GetSection(DevToolsOptions.Section).Get<DevToolsOptions>() ?? new DevToolsOptions();
builder.Services.AddSingleton(devTools);
builder.Services.AddSingleton<DevCallRegistry>();
builder.Services.AddHttpClient("evocpp", c =>
{
    c.BaseAddress = new Uri(devTools.EvocppBaseUrl);
    c.Timeout     = TimeSpan.FromSeconds(10);
});
builder.Services.AddHostedService<OcppEventConsumer>();
builder.Services.AddHostedService<StaleSessionWatchdog>();

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(config.GetConnectionString("Default")));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opt =>
    {
        opt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = config["Jwt:Issuer"],
            ValidAudience            = config["Jwt:Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!)),
        };

        // SignalR sends the JWT in the query string on WebSocket upgrade.
        opt.Events = new JwtBearerEvents
        {
            OnMessageReceived = ctx =>
            {
                var token = ctx.Request.Query["access_token"];
                if (!string.IsNullOrEmpty(token) && ctx.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                    ctx.Token = token;
                return Task.CompletedTask;
            },
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddRateLimiter(opt =>
{
    opt.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    opt.AddPolicy(RateLimitPolicies.Auth, ctx => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPolicies.ClientKey(ctx),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("RateLimit:AuthPerMinute", 10),
            Window      = TimeSpan.FromMinutes(1),
        }));

    opt.AddPolicy(RateLimitPolicies.Claim, ctx => RateLimitPartition.GetFixedWindowLimiter(
        RateLimitPolicies.ClientKey(ctx),
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = config.GetValue("RateLimit:ClaimPerMinute", 5),
            Window      = TimeSpan.FromMinutes(1),
        }));
});

var app = builder.Build();

// ── Database ──────────────────────────────────────────────────────────────────
// Tests swap in the InMemory provider: build the schema from the model, no migrations.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
}

// ── Pipeline ──────────────────────────────────────────────────────────────────
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHub<ChargerHub>("/hubs/charger");

app.Run();

public partial class Program;   // for WebApplicationFactory<Program> in tests
