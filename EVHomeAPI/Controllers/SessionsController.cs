using EVHomeAPI.Data;
using EVHomeAPI.DTOs;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Controllers;

[ApiController]
[Authorize]
[Route("api/sessions")]
public class SessionsController(AppDbContext db) : ControllerBase
{
    /// <summary>404 when the session does not exist or belongs to a station the user cannot access.</summary>
    [HttpGet("{id:int}")]
    public async Task<ActionResult<SessionDto>> Get(int id)
    {
        var session = await AccessibleSessions().FirstOrDefaultAsync(s => s.Id == id);
        return session is null ? NotFound() : Ok(SessionDto.From(session));
    }

    /// <summary>Chart samples (~one per 30 s), oldest first — same shape as Nabla's meter-history.</summary>
    [HttpGet("{id:int}/meter-history")]
    public async Task<ActionResult<List<MeterPointDto>>> GetMeterHistory(int id)
    {
        if (!await AccessibleSessions().AnyAsync(s => s.Id == id)) return NotFound();
        var points = await db.MeterReadings
            .Where(r => r.SessionId == id)
            .OrderBy(r => r.ElapsedSec)
            .Select(r => new MeterPointDto(r.ElapsedSec, r.CurrentPowerKw, r.Soc))
            .ToListAsync();
        return Ok(points);
    }

    private IQueryable<Models.ChargingSession> AccessibleSessions()
    {
        var userId = User.GetUserId();
        return db.Sessions.Where(s => db.StationAccesses.Any(a => a.StationId == s.StationId && a.UserId == userId));
    }
}
