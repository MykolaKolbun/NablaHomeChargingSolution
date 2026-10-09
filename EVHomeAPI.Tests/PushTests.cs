using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EVHomeAPI.Controllers;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Push;
using EVHomeAPI.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Tests;

public class PushTests
{
    private const string Id = "30011";

    private static Task Status(ApiFactory f, string status, int connector = 1, bool connected = true) =>
        f.PublishEventAsync(OcppRoutingKeys.StatusChanged, new StatusChangedEvent(Id, status, connector, connected, null));

    [Fact]
    public async Task Power_loss_cycle_enqueues_paused_then_resumed()
    {
        using var f = new ApiFactory();
        await f.CreateOwnedStationAsync(Id);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 0m));
        await f.PublishEventAsync(OcppRoutingKeys.MeterUpdated, new MeterUpdatedEvent(Id, 1_000m, 0m, 3.5, null));

        await Status(f, "Offline", connector: 0, connected: false);
        await Status(f, "Available", connector: 0);
        await Status(f, "Finishing");
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 1, 1_000m, 0m, "PowerLoss"));
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 995m));

        Assert.Equal([PushKind.Paused, PushKind.Resumed], f.Push.Sent.Select(p => p.Kind));   // one Paused, not two
        Assert.Equal(1.0m, f.Push.Sent.Last().EnergyKwh);
    }

    [Fact]
    public async Task Unplugged_during_outage_enqueues_resume_failed()
    {
        using var f = new ApiFactory();
        await f.CreateOwnedStationAsync(Id);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 0m));
        await Status(f, "Offline", connector: 0, connected: false);
        await Status(f, "Available", connector: 0);
        await Status(f, "Available");
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 1, 500m, 0m, "PowerLoss"));

        var last = f.Push.Sent.Last();
        Assert.Equal(PushKind.ResumeFailed, last.Kind);
        Assert.Equal("Unplugged", last.Detail);
    }

    [Fact]
    public async Task Completed_pushed_only_when_not_stopped_from_app()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 1, 0m));
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 1, 7_000m, 0m, "EVDisconnected"));
        Assert.Equal(PushKind.Completed, Assert.Single(f.Push.Sent).Kind);

        await f.PublishEventAsync(OcppRoutingKeys.TransactionStarted, new TransactionStartedEvent(Id, 2, 7_000m));
        await owner.PostAsync($"/api/stations/{stationId}/stop", null);
        await f.PublishEventAsync(OcppRoutingKeys.TransactionStopped, new TransactionStoppedEvent(Id, 2, 8_000m, 7_000m, "Remote"));
        Assert.Single(f.Push.Sent);
    }

    [Fact]
    public async Task Device_register_moves_token_between_accounts_and_unregister_removes_it()
    {
        using var f = new ApiFactory();
        var a = await f.CreateUserClientAsync();
        var b = await f.CreateUserClientAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await a.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest("tok-1", "android", "en"))).StatusCode);
        await a.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest("tok-1", "android", "en"));   // idempotent
        await f.WithDbAsync(async db => Assert.Equal("en", (await db.DeviceTokens.SingleAsync()).Language));

        await b.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest("tok-1", null, null));
        int userA = 0, owner = 0;
        await f.WithDbAsync(async db =>
        {
            var d = await db.DeviceTokens.SingleAsync();
            owner = d.UserId;
            userA = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
            Assert.Equal("uk", d.Language);
        });
        Assert.NotEqual(userA, owner);

        await a.PostAsJsonAsync("/api/devices/unregister", new UnregisterDeviceRequest("tok-1"));   // not A's any more
        await f.WithDbAsync(async db => Assert.Equal(1, await db.DeviceTokens.CountAsync()));
        await b.PostAsJsonAsync("/api/devices/unregister", new UnregisterDeviceRequest("tok-1"));
        await f.WithDbAsync(async db => Assert.Equal(0, await db.DeviceTokens.CountAsync()));

        Assert.Equal(HttpStatusCode.Unauthorized, (await f.CreateClient().PostAsJsonAsync("/api/devices", new RegisterDeviceRequest("x", null, null))).StatusCode);
    }

    [Fact]
    public async Task Worker_sends_to_station_users_only_and_drops_invalid_tokens()
    {
        using var f = new ApiFactory();
        var (owner, stationId) = await f.CreateOwnedStationAsync(Id);
        var stranger = await f.CreateUserClientAsync();
        await owner.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest("owner-phone", "android", "uk"));
        await owner.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest("owner-old", "android", "uk"));
        await stranger.PostAsJsonAsync("/api/devices", new RegisterDeviceRequest("stranger", "android", "uk"));

        var sender = new FakePushSender();
        sender.Invalid.Add("owner-old");
        await f.WithDbAsync(db => PushWorker.DeliverAsync(db, sender, new PushEvent(stationId, PushKind.Paused, 1.3m)));

        var sent = Assert.Single(sender.Sent);
        Assert.Equal("owner-phone", sent.Token);
        Assert.Contains("Garage", sent.Body);
        await f.WithDbAsync(async db =>
            Assert.Equal(["owner-phone", "stranger"], await db.DeviceTokens.OrderBy(d => d.Token).Select(d => d.Token).ToListAsync()));
    }

    [Theory]
    [InlineData(PushKind.Paused, null, "uk", "призупинено")]
    [InlineData(PushKind.Resumed, null, "uk", "2.5 кВт·год")]
    [InlineData(PushKind.ResumeFailed, "Unplugged", "uk", "кабель")]
    [InlineData(PushKind.ResumeFailed, "NotRestarted", "en", "check the car")]
    [InlineData(PushKind.Completed, null, "en", "2.5 kWh")]
    public void Texts_render_per_language(PushKind kind, string? detail, string lang, string expected)
    {
        var (title, body) = PushTexts.Render(new PushEvent(1, kind, 2.5m, detail), "Garage", lang);
        Assert.Contains(expected, title + " " + body);
        Assert.StartsWith("Garage", body);
    }

    [Fact]
    public void Fcm_assertion_is_a_valid_rs256_jwt()
    {
        using var rsa = RSA.Create(2048);
        var account = new FcmSender.ServiceAccount("nabla-home", "push@nabla-home.iam.gserviceaccount.com",
            rsa.ExportPkcs8PrivateKeyPem(), "https://oauth2.googleapis.com/token");
        var now = new DateTimeOffset(2026, 10, 10, 0, 0, 0, TimeSpan.Zero);

        var jwt   = FcmSender.CreateAssertion(account, "https://oauth2.googleapis.com/token", now);
        var parts = jwt.Split('.');
        Assert.Equal(3, parts.Length);

        var claims = JsonDocument.Parse(FromB64Url(parts[1])).RootElement;
        Assert.Equal("push@nabla-home.iam.gserviceaccount.com", claims.GetProperty("iss").GetString());
        Assert.Equal("https://www.googleapis.com/auth/firebase.messaging", claims.GetProperty("scope").GetString());
        Assert.Equal(3600, claims.GetProperty("exp").GetInt64() - claims.GetProperty("iat").GetInt64());
        Assert.True(rsa.VerifyData(Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}"), FromB64Url(parts[2]),
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
    }

    [Fact]
    public void Sender_without_credentials_is_disabled()
    {
        var sender = new FcmSender(new HttpClient(), new FcmOptions(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<FcmSender>.Instance);
        Assert.False(sender.Enabled);
    }

    private static byte[] FromB64Url(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
