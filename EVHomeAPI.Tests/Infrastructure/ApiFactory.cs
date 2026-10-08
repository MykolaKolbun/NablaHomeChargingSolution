using System.Net.Http.Headers;
using System.Net.Http.Json;
using EVHomeAPI.Data;
using EVHomeAPI.DTOs;
using Microsoft.AspNetCore.Hosting;
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
        });
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
