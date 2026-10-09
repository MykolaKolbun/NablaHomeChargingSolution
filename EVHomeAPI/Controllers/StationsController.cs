using EVHomeAPI.Data;
using EVHomeAPI.DTOs;
using EVHomeAPI.Models;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/stations")]
public class StationsController(
    AppDbContext db,
    IOcppCommandPublisher commands,
    TimeProvider clock,
    ILogger<StationsController> logger) : ControllerBase
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    /// <summary>Stations the current user owns or is a member of.</summary>
    [HttpGet]
    public async Task<ActionResult<List<StationDto>>> GetMine()
    {
        var userId = User.GetUserId();
        var list = await db.StationAccesses
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Station.Name)
            .Select(a => StationDto.From(a.Station, a.Role))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>404 when the station does not exist or the user has no access (no existence leak).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StationDto>> Get(int id)
    {
        var access = await FindAccess(id);
        return access is null ? NotFound() : Ok(StationDto.From(access.Station, access.Role));
    }

    /// <summary>
    /// Become the Owner of a station by presenting its OcppId and one-time claim code.
    /// Unknown station, wrong code and already-claimed all return the same 400, so the
    /// endpoint cannot be used to discover which station IDs exist.
    /// </summary>
    [HttpPost("claim")]
    [EnableRateLimiting(RateLimitPolicies.Claim)]
    public async Task<ActionResult<StationDto>> Claim(ClaimStationRequest req)
    {
        const string invalid = "Invalid station ID or claim code.";
        var userId  = User.GetUserId();
        var station = await db.Stations.FirstOrDefaultAsync(s => s.OcppId == req.OcppId.Trim());

        if (station?.ClaimCodeHash is null ||
            !BCrypt.Net.BCrypt.Verify(ClaimCode.Normalize(req.ClaimCode), station.ClaimCodeHash))
            return BadRequest(invalid);

        station.ClaimCodeHash = null;          // one-time
        station.ClaimedAt     = Now;
        db.StationAccesses.Add(new StationAccess { StationId = station.Id, UserId = userId, Role = StationRole.Owner });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return BadRequest(invalid);        // someone else claimed it in the meantime
        }

        await commands.TryRequestStatusAsync(station.OcppId, logger);   // fresh online/connector state for the new owner
        return Ok(StationDto.From(station, StationRole.Owner));
    }

    // ── Charging ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Ask the charger to start (RemoteStartTransaction). Returns 202 with a Pending
    /// session; the outcome arrives over SignalR (SessionStarted / SessionStartFailed).
    /// </summary>
    [HttpPost("{id:int}/start")]
    public async Task<ActionResult<SessionDto>> Start(int id, [FromBody] StartChargingRequest? req)
    {
        var access = await FindAccess(id);
        if (access is null) return NotFound();
        var station = access.Station;

        if (!station.IsOnline) return Conflict("Station is offline.");
        if (await HasOpenSession(id)) return Conflict("A session is already in progress.");

        var session = new ChargingSession
        {
            StationId   = id,
            UserId      = access.UserId,
            ConnectorId = req?.ConnectorId ?? 1,
            InitiatedBy = SessionInitiator.App,
            Status      = SessionStatus.Pending,
            TrackingId  = Guid.NewGuid(),
            CreatedAt   = Now,
        };
        db.Sessions.Add(session);
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            return Conflict("A session is already in progress.");   // lost the race (IX_Sessions_OneOpenPerStation)
        }

        try
        {
            // OCPP 1.6 idTag is CiString20 — "U{userId}" identifies who started it.
            await commands.RemoteStartAsync(new RemoteStartCommand(station.OcppId, session.ConnectorId, $"U{access.UserId}", session.TrackingId.Value));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RemoteStart publish failed for station {OcppId}", station.OcppId);
            session.Status  = SessionStatus.Cancelled;
            session.EndedAt = Now;
            await db.SaveChangesAsync();
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Charger gateway unavailable.");
        }

        return Accepted(SessionDto.From(session));
    }

    /// <summary>
    /// Ask the charger to stop (RemoteStopTransaction). Returns 202 with a Stopping
    /// session; the outcome arrives over SignalR (SessionFinalized / SessionStopFailed).
    /// Idempotent while already Stopping.
    /// </summary>
    [HttpPost("{id:int}/stop")]
    public async Task<ActionResult<SessionDto>> Stop(int id)
    {
        if (await FindAccess(id) is not { } access) return NotFound();

        var session = await OpenSessionQuery(id).FirstOrDefaultAsync();
        if (session is null) return Conflict("No session in progress.");
        if (session.Status == SessionStatus.Pending) return Conflict("Session is still starting.");
        if (session.Status == SessionStatus.Stopping) return Accepted(SessionDto.From(session));

        session.Status          = SessionStatus.Stopping;
        session.StopRequestedAt = Now;
        await db.SaveChangesAsync();

        try
        {
            await commands.RemoteStopAsync(new RemoteStopCommand(access.Station.OcppId, session.OcppTransactionId));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "RemoteStop publish failed for station {OcppId}", access.Station.OcppId);
            session.Status          = SessionStatus.Active;
            session.StopRequestedAt = null;
            await db.SaveChangesAsync();
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Charger gateway unavailable.");
        }

        return Accepted(SessionDto.From(session));
    }

    /// <summary>Session in progress (Pending/Active/Stopping), or 204 when idle.</summary>
    [HttpGet("{id:int}/session")]
    public async Task<ActionResult<SessionDto>> GetOpenSession(int id)
    {
        if (await FindAccess(id) is null) return NotFound();
        var session = await OpenSessionQuery(id).FirstOrDefaultAsync();
        return session is null ? NoContent() : Ok(SessionDto.From(session));
    }

    /// <summary>Most recent sessions first.</summary>
    [HttpGet("{id:int}/sessions")]
    public async Task<ActionResult<List<SessionDto>>> GetHistory(int id, [FromQuery] int limit = 50)
    {
        if (await FindAccess(id) is null) return NotFound();
        var sessions = await db.Sessions
            .Where(s => s.StationId == id)
            .OrderByDescending(s => s.Id)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync();
        return Ok(sessions.Select(SessionDto.From).ToList());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Task<StationAccess?> FindAccess(int stationId)
    {
        var userId = User.GetUserId();
        return db.StationAccesses
            .Include(a => a.Station)
            .FirstOrDefaultAsync(a => a.StationId == stationId && a.UserId == userId);
    }

    private IQueryable<ChargingSession> OpenSessionQuery(int stationId) =>
        db.Sessions
            .Where(s => s.StationId == stationId &&
                        (s.Status == SessionStatus.Pending || s.Status == SessionStatus.Active || s.Status == SessionStatus.Stopping))
            .OrderByDescending(s => s.Id);

    private Task<bool> HasOpenSession(int stationId) => OpenSessionQuery(stationId).AnyAsync();
}
