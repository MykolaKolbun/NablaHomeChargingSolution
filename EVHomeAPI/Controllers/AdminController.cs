using EVHomeAPI.Data;
using EVHomeAPI.DTOs;
using EVHomeAPI.Models;
using EVHomeAPI.Ocpp;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Controllers;

/// <summary>
/// Operator endpoints, protected by the shared secret in header X-Admin-Key (config AdminKey).
/// Stage 1 has no admin UI — call with curl.
/// </summary>
[ApiController]
[AdminKey]
[Route("api/admin")]
public class AdminController(AppDbContext db, IOcppCommandPublisher commands, ChargingLimitService limits, ILogger<AdminController> logger) : ControllerBase
{
    /// <summary>Register a station and get its one-time claim code (shown only in this response).</summary>
    [HttpPost("stations")]
    public async Task<ActionResult<CreateStationResponse>> CreateStation(CreateStationRequest req)
    {
        var ocppId = req.OcppId.Trim();
        if (await db.Stations.AnyAsync(s => s.OcppId == ocppId))
            return Conflict($"Station '{ocppId}' already exists.");

        var code    = ClaimCode.Generate();
        var station = new Station
        {
            OcppId        = ocppId,
            Name          = req.Name.Trim(),
            MaxCurrentA   = req.MaxCurrentA,
            ClaimCodeHash = BCrypt.Net.BCrypt.HashPassword(ClaimCode.Normalize(code)),
        };
        db.Stations.Add(station);
        await db.SaveChangesAsync();
        await commands.TryRequestStatusAsync(station.OcppId, logger);   // charger may already be online

        return Ok(new CreateStationResponse(station.Id, station.OcppId, station.Name, code));
    }

    /// <summary>Issue a new claim code (e.g. lost sticker). Existing access is kept.</summary>
    [HttpPost("stations/{ocppId}/claim-code")]
    public async Task<ActionResult<CreateStationResponse>> ResetClaimCode(string ocppId)
    {
        var station = await db.Stations.FirstOrDefaultAsync(s => s.OcppId == ocppId);
        if (station is null) return NotFound();

        var code = ClaimCode.Generate();
        station.ClaimCodeHash = BCrypt.Net.BCrypt.HashPassword(ClaimCode.Normalize(code));
        await db.SaveChangesAsync();

        return Ok(new CreateStationResponse(station.Id, station.OcppId, station.Name, code));
    }

    /// <summary>
    /// Change the installation maximum (cable / breaker / house supply). A user limit above
    /// the new maximum is lowered to it. If the station is online the effective limit is
    /// pushed to the charger right away (also to a running session).
    /// </summary>
    [HttpPut("stations/{ocppId}/max-current")]
    public async Task<ActionResult<object>> SetMaxCurrent(string ocppId, SetMaxCurrentRequest req)
    {
        var station = await db.Stations.FirstOrDefaultAsync(s => s.OcppId == ocppId);
        if (station is null) return NotFound();

        station.MaxCurrentA = req.MaxCurrentA;
        if (station.CurrentLimitA > req.MaxCurrentA) station.CurrentLimitA = req.MaxCurrentA;
        await db.SaveChangesAsync();

        var pushed = station.IsOnline && await limits.ApplyAsync(station);
        return Ok(new { station.OcppId, station.MaxCurrentA, station.CurrentLimitA, station.LimitStatus, pushed });
    }
}
