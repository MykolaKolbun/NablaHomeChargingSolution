using System.ComponentModel.DataAnnotations;
using EVHomeAPI.Data;
using EVHomeAPI.Models;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Controllers;

public record RegisterDeviceRequest(
    [Required, MaxLength(512)] string Token,
    [MaxLength(16)] string? Platform,
    [MaxLength(8)]  string? Language);

public record UnregisterDeviceRequest([Required, MaxLength(512)] string Token);

/// <summary>FCM tokens of the current user's devices (push notifications).</summary>
[ApiController]
[Authorize]
[Route("api/devices")]
public class DevicesController(AppDbContext db, TimeProvider clock) : ControllerBase
{
    /// <summary>
    /// Register or refresh this device's token. A token belongs to one install, so if it was
    /// registered by another account (logout → login as someone else) it moves to the caller.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Register(RegisterDeviceRequest req)
    {
        var userId = User.GetUserId();
        var now    = clock.GetUtcNow().UtcDateTime;
        var lang   = req.Language is "en" ? "en" : "uk";

        var device = await db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == req.Token);
        if (device is null)
        {
            device = new DeviceToken { Token = req.Token, CreatedAt = now };
            db.DeviceTokens.Add(device);
        }
        device.UserId     = userId;
        device.Platform   = req.Platform ?? "android";
        device.Language   = lang;
        device.LastSeenAt = now;
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>Called on logout so the device stops receiving this account's pushes.</summary>
    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister(UnregisterDeviceRequest req)
    {
        var userId = User.GetUserId();
        var device = await db.DeviceTokens.FirstOrDefaultAsync(d => d.Token == req.Token && d.UserId == userId);
        if (device is not null)
        {
            db.DeviceTokens.Remove(device);
            await db.SaveChangesAsync();
        }
        return NoContent();
    }
}
