using System.Text.Json;
using EVHomeAPI.Data;
using EVHomeAPI.Models;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Push;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Controllers;

public record DevCallRequest(string Action, JsonElement? Payload);

public record DevPushRequest(string Kind);

/// <summary>Enabled = FCM credentials configured; Devices = registered devices of the station users; Sent = accepted by FCM.</summary>
public record DevPushResult(bool Enabled, int Devices, int Sent);

/// <summary>Raw charger answer. Status: Ok | Invalid | NotConnected | NotSupported | Timeout | Error.</summary>
public record DevCallResult(string Action, string Status, JsonElement? Result, int ElapsedMs);

/// <summary>
/// Developer tooling for charger bring-up (EVHomeApp "Developer" screen). Owner only.
/// The whole controller answers 404 unless DevTools:Enabled = true, so production can switch it off.
/// </summary>
[ApiController]
[Authorize]
[Route("api/stations/{id:int}/dev")]
public class DevController(
    AppDbContext db,
    IOcppCommandPublisher commands,
    DevCallRegistry registry,
    DevToolsOptions options,
    IHttpClientFactory http,
    ILogger<DevController> logger) : ControllerBase
{
    /// <summary>
    /// Sends a whitelisted OCPP call to the charger and waits for its raw answer
    /// (up to DevTools:CallTimeoutSeconds). 504 when nothing comes back.
    /// </summary>
    [HttpPost("call")]
    public async Task<ActionResult<DevCallResult>> Call(int id, DevCallRequest req, CancellationToken ct)
    {
        if (await GuardAsync(id) is { } deny) return deny;
        if (!DevCallActions.Allowed.Contains(req.Action)) return BadRequest($"Action '{req.Action}' is not allowed.");

        var ocppId    = await db.Stations.Where(s => s.Id == id).Select(s => s.OcppId).SingleAsync(ct);
        var requestId = Guid.NewGuid().ToString("N");
        var started   = DateTime.UtcNow;
        var pending   = registry.Register(requestId);

        try
        {
            await commands.DevCallAsync(new DevCallCommand(ocppId, requestId, req.Action, req.Payload), ct);
            var response = await pending.WaitAsync(TimeSpan.FromSeconds(options.CallTimeoutSeconds), ct);
            return Ok(new DevCallResult(req.Action, response.Status, response.Result, Elapsed(started)));
        }
        catch (TimeoutException)
        {
            return StatusCode(StatusCodes.Status504GatewayTimeout, new DevCallResult(req.Action, "Timeout", null, Elapsed(started)));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Dev call {Action} to {OcppId} failed", req.Action, ocppId);
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Charger gateway unavailable.");
        }
        finally
        {
            registry.Forget(requestId);
        }
    }

    /// <summary>EVOCPP's view of the charger (firmware, meterType, protocol, last heartbeat…), raw JSON.</summary>
    [HttpGet("charger")]
    public async Task<IActionResult> Charger(int id, CancellationToken ct)
    {
        if (await GuardAsync(id) is { } deny) return deny;
        var ocppId = await db.Stations.Where(s => s.Id == id).Select(s => s.OcppId).SingleAsync(ct);

        try
        {
            using var res  = await http.CreateClient("evocpp").GetAsync($"/api/admin/plugs/{Uri.EscapeDataString(ocppId)}", ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            return new ContentResult { StatusCode = (int)res.StatusCode, Content = body, ContentType = "application/json" };
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "EVOCPP plug info for {OcppId} unavailable", ocppId);
            return StatusCode(StatusCodes.Status502BadGateway, "EVOCPP unavailable.");
        }
    }

    /// <summary>
    /// Sends a sample push (kind: Paused | Resumed | ResumeFailed | Completed) to every device of
    /// the station's users, synchronously, and reports how many FCM accepted.
    /// </summary>
    [HttpPost("push")]
    public async Task<ActionResult<DevPushResult>> Push(int id, DevPushRequest req, [FromServices] IPushSender sender, CancellationToken ct)
    {
        if (await GuardAsync(id) is { } deny) return deny;
        if (!Enum.TryParse<PushKind>(req.Kind, out var kind)) return BadRequest("Unknown kind.");

        var devices = await db.DeviceTokens
            .CountAsync(d => db.StationAccesses.Any(a => a.StationId == id && a.UserId == d.UserId), ct);
        var sent = sender.Enabled
            ? await PushWorker.DeliverAsync(db, sender, new PushEvent(id, kind, 12.3m, kind == PushKind.ResumeFailed ? "NotRestarted" : null), ct)
            : 0;
        return Ok(new DevPushResult(sender.Enabled, devices, sent));
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<ActionResult?> GuardAsync(int stationId)
    {
        if (!options.Enabled) return NotFound();
        var userId = User.GetUserId();
        var role   = await db.StationAccesses
            .Where(a => a.StationId == stationId && a.UserId == userId)
            .Select(a => (StationRole?)a.Role)
            .FirstOrDefaultAsync();
        if (role is null) return NotFound();
        return role == StationRole.Owner ? null : Forbid();
    }

    private static int Elapsed(DateTime started) => (int)(DateTime.UtcNow - started).TotalMilliseconds;
}
