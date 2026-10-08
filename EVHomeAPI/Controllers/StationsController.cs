using EVHomeAPI.Data;
using EVHomeAPI.DTOs;
using EVHomeAPI.Models;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/stations")]
public class StationsController(AppDbContext db) : ControllerBase
{
    /// <summary>Stations the current user owns or is a member of.</summary>
    [HttpGet]
    public async Task<ActionResult<List<StationDto>>> GetMine()
    {
        var userId = User.GetUserId();
        var list = await db.StationAccesses
            .Where(a => a.UserId == userId)
            .OrderBy(a => a.Station.Name)
            .Select(a => new StationDto(a.Station.Id, a.Station.OcppId, a.Station.Name, a.Role.ToString(), a.Station.ClaimedAt))
            .ToListAsync();
        return Ok(list);
    }

    /// <summary>404 when the station does not exist or the user has no access (no existence leak).</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<StationDto>> Get(int id)
    {
        var userId = User.GetUserId();
        var dto = await db.StationAccesses
            .Where(a => a.UserId == userId && a.StationId == id)
            .Select(a => new StationDto(a.Station.Id, a.Station.OcppId, a.Station.Name, a.Role.ToString(), a.Station.ClaimedAt))
            .FirstOrDefaultAsync();
        return dto is null ? NotFound() : Ok(dto);
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
        station.ClaimedAt     = DateTime.UtcNow;
        db.StationAccesses.Add(new StationAccess { StationId = station.Id, UserId = userId, Role = StationRole.Owner });
        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return BadRequest(invalid);        // someone else claimed it in the meantime
        }

        return Ok(new StationDto(station.Id, station.OcppId, station.Name, nameof(StationRole.Owner), station.ClaimedAt));
    }
}
