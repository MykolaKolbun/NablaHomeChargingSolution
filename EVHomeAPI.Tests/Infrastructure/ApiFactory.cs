using System.Net.Http.Headers;
using System.Net.Http.Json;
using EVHomeAPI.Data;
using EVHomeAPI.DTOs;
using EVHomeAPI.Hubs;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EVHomeAPI.Tests.Infrastructure;

/// <summary>
/// Runs EVHomeAPI in-process with an isolated InMemory database per factory and the
/// real JWT pipeline (tests register/login and use real tokens).
/// </summary>
public class ApiFactory : WebApplicationFactory<Program>
{
    public const string AdminKey = "test-admin-key-0123456789abcdef";

    private readonly string _dbName = Guid.NewGuid().ToString();
    private readonly int    _claimPerMinute;

    public ApiFactory(int claimPerMinute = 1000) => _claimPerMinute = claimPerMinute;

    /// <summary>Dev tools on by default in tests; CallTimeoutSeconds short so timeout tests are fast.</summary>
    public DevToolsOptions   DevTools { get; } = new() { Enabled = true, CallTimeoutSeconds = 2, EvocppBaseUrl = "http://evocpp.test" };
    public FakeEvocppHandler Evocpp   { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("AdminKey", AdminKey);
        builder.UseSetting("RateLimit:AuthPerMinute",  "1000");
        builder.UseSetting("RateLimit:ClaimPerMinute", _claimPerMinute.ToString());

        builder.ConfigureTestServices(services =>
        {
            // Replace Npgsql with InMemory: remove every AppDbContext / options registration
            // first, otherwise EF sees two providers and refuses to start.
            foreach (var d in services.Where(s =>
                         s.ServiceType == typeof(AppDbContext) ||
                         s.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                         s.ServiceType == typeof(DbContextOptions)).ToList())
                services.Remove(d);

            var options = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(_dbName).Options;
            services.AddSingleton(options);
            services.AddScoped<AppDbContext>();

            // No RabbitMQ in tests: drop background services, record commands/pushes instead.
            foreach (var d in services.Where(s => s.ServiceType == typeof(IHostedService) &&
                         (s.ImplementationType == typeof(OcppEventConsumer) ||
                          s.ImplementationType == typeof(StaleSessionWatchdog))).ToList())
                services.Remove(d);
            Replace<IOcppCommandPublisher>(services, Commands);
            Replace<INotifier>(services, Notifier);
            Replace<TimeProvider>(services, Clock);
            Replace<DevToolsOptions>(services, DevTools);
            services.AddHttpClient("evocpp").ConfigurePrimaryHttpMessageHandler(() => Evocpp);
        });
    }

    private static void Replace<T>(IServiceCollection services, T instance) where T : class
    {
        foreach (var d in services.Where(s => s.ServiceType == typeof(T)).ToList())
            services.Remove(d);
        services.AddSingleton(instance);
    }

    public FakeCommandPublisher Commands { get; } = new();
    public FakeNotifier         Notifier { get; } = new();
    public ManualTimeProvider   Clock    { get; } = new(new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero));

    /// <summary>Feeds one EVOCPP event through the real processor (as the consumer would).</summary>
    public async Task PublishEventAsync(string routingKey, object payload)
    {
        await using var scope = Services.CreateAsyncScope();
        var json = System.Text.Json.JsonSerializer.Serialize(payload,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        await scope.ServiceProvider.GetRequiredService<OcppEventProcessor>().HandleAsync(routingKey, json);
    }

    public async Task WithDbAsync(Func<AppDbContext, Task> action)
    {
        await using var scope = Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    /// <summary>Creates station → registers a user → claims it. Returns the owner client and station id.</summary>
    public async Task<(HttpClient Owner, int StationId)> CreateOwnedStationAsync(string ocppId = "30011", bool online = true)
    {
        var created = await CreateStationAsync(ocppId);
        var owner   = await CreateUserClientAsync();
        (await owner.PostAsJsonAsync("/api/stations/claim", new ClaimStationRequest(ocppId, created.ClaimCode))).EnsureSuccessStatusCode();
        if (online)
            await PublishEventAsync(OcppRoutingKeys.StatusChanged,
                new StatusChangedEvent(ocppId, "Available", 1, true, null));
        return (owner, created.Id);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Registers a fresh user and returns a client authenticated as them.</summary>
    public async Task<HttpClient> CreateUserClientAsync(string? email = null)
    {
        var client = CreateClient();
        var res = await client.PostAsJsonAsync("/api/auth/register",
            new RegisterRequest("Test User", email ?? $"{Guid.NewGuid():N}@test.local", "password123"));
        res.EnsureSuccessStatusCode();
        var auth = await res.Content.ReadFromJsonAsync<AuthResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth!.Token);
        return client;
    }

    public HttpClient CreateAdminClient()
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add("X-Admin-Key", AdminKey);
        return client;
    }

    /// <summary>Creates a station via the admin API and returns its one-time claim code.</summary>
    public async Task<CreateStationResponse> CreateStationAsync(string ocppId, string name = "Garage")
    {
        var res = await CreateAdminClient().PostAsJsonAsync("/api/admin/stations", new CreateStationRequest(ocppId, name));
        res.EnsureSuccessStatusCode();
        return (await res.Content.ReadFromJsonAsync<CreateStationResponse>())!;
    }
}
