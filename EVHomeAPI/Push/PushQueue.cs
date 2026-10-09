using System.Threading.Channels;
using EVHomeAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Push;

public enum PushKind
{
    Paused,          // charger lost power / went offline mid-session
    Resumed,         // charging continues in the same session
    ResumeFailed,    // paused session ended (unplugged, attempts exhausted, too long)
    Completed,       // finished by the charger or the car (not by the user in the app)
}

/// <summary>Detail for ResumeFailed: Unplugged | NotRestarted | TooLong.</summary>
public record PushEvent(int StationId, PushKind Kind, decimal EnergyKwh, string? Detail = null);

public interface IPushQueue
{
    void Enqueue(PushEvent e);
}

/// <summary>
/// In-memory queue so OCPP event processing never waits for FCM. Best effort: a push lost on
/// restart is acceptable (the app shows the real state on open).
/// </summary>
public sealed class PushQueue : IPushQueue
{
    private readonly Channel<PushEvent> _channel = Channel.CreateBounded<PushEvent>(
        new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });

    public void Enqueue(PushEvent e) => _channel.Writer.TryWrite(e);
    public ChannelReader<PushEvent> Reader => _channel.Reader;
}

/// <summary>Delivers queued events to every device of every user with access to the station.</summary>
public sealed class PushWorker(PushQueue queue, IServiceScopeFactory scopes, IPushSender sender, ILogger<PushWorker> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var e in queue.Reader.ReadAllAsync(ct))
        {
            if (!sender.Enabled) continue;
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await DeliverAsync(scope.ServiceProvider.GetRequiredService<AppDbContext>(), sender, e, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Push delivery failed for station {StationId} ({Kind})", e.StationId, e.Kind);
            }
        }
    }

    public static async Task DeliverAsync(AppDbContext db, IPushSender sender, PushEvent e, CancellationToken ct = default)
    {
        var station = await db.Stations.FirstOrDefaultAsync(s => s.Id == e.StationId, ct);
        if (station is null) return;

        var devices = await db.DeviceTokens
            .Where(d => db.StationAccesses.Any(a => a.StationId == e.StationId && a.UserId == d.UserId))
            .ToListAsync(ct);

        foreach (var d in devices)
        {
            var (title, body) = PushTexts.Render(e, station.Name, d.Language);
            var data = new Dictionary<string, string> { ["stationId"] = e.StationId.ToString(), ["kind"] = e.Kind.ToString() };
            if (await sender.SendAsync(d.Token, title, body, data, ct) == PushResult.InvalidToken)
                db.DeviceTokens.Remove(d);
        }
        await db.SaveChangesAsync(ct);
    }
}

public static class PushTexts
{
    public static (string Title, string Body) Render(PushEvent e, string station, string language)
    {
        var kwh = e.EnergyKwh.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
        var uk  = language != "en";
        return e.Kind switch
        {
            PushKind.Paused => uk
                ? ("⏸ Заряд призупинено", $"{station}: зарядка без живлення. Продовжимо автоматично, щойно світло повернеться.")
                : ("⏸ Charging paused",   $"{station}: the charger has no power. Charging resumes automatically when power is back."),
            PushKind.Resumed => uk
                ? ("▶ Заряд продовжено", $"{station}: заряджання триває. Уже {kwh} кВт·год.")
                : ("▶ Charging resumed", $"{station}: charging continues. {kwh} kWh so far."),
            PushKind.ResumeFailed => (uk ? "⚠ Заряд не продовжено" : "⚠ Charging not resumed", e.Detail switch
            {
                "Unplugged" => uk ? $"{station}: кабель від'єднали під час відключення. Заряджено {kwh} кВт·год."
                                  : $"{station}: the cable was unplugged during the outage. {kwh} kWh charged.",
                "TooLong"   => uk ? $"{station}: світла немає понад добу, сесію завершено. Заряджено {kwh} кВт·год."
                                  : $"{station}: no power for over a day, session ended. {kwh} kWh charged.",
                _           => uk ? $"{station}: зарядка не змогла відновити заряд — перевірте авто. Заряджено {kwh} кВт·год."
                                  : $"{station}: the charger could not restart charging — check the car. {kwh} kWh charged.",
            }),
            _ => uk
                ? ("✅ Заряд завершено", $"{station}: заряджено {kwh} кВт·год.")
                : ("✅ Charging finished", $"{station}: {kwh} kWh charged."),
        };
    }
}
