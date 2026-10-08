using EVHomeAPI.Data;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Hubs;

/// <summary>
/// Real-time channel for EVHomeApp: /hubs/charger (JWT via ?access_token=).
/// Clients call JoinStation(stationId) and receive the station's events — same
/// event names and payloads as the commercial Nabla hub (see INotifier).
/// </summary>
[Authorize]
public class ChargerHub(AppDbContext db) : Hub
{
    public static string Group(int stationId) => $"station-{stationId}";

    public async Task JoinStation(int stationId)
    {
        var userId = Context.User!.GetUserId();
        var allowed = await db.StationAccesses.AnyAsync(a => a.StationId == stationId && a.UserId == userId);
        if (!allowed) throw new HubException("No access to this station.");
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(stationId));
    }

    public Task LeaveStation(int stationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(stationId));
}
