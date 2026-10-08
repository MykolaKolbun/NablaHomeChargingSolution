using EVHomeAPI.Data;
using Microsoft.AspNetCore.Mvc;

namespace EVHomeAPI.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(AppDbContext db) : ControllerBase
{
    /// <summary>200 when the API and its database are reachable, 503 otherwise.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        var body = new { status = dbOk ? "ok" : "degraded", db = dbOk };
        return dbOk ? Ok(body) : StatusCode(StatusCodes.Status503ServiceUnavailable, body);
    }
}
