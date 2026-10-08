using Microsoft.AspNetCore.SignalR;

namespace EVHomeAPI.Hubs;

// Payloads mirror the commercial Nabla SignalR contract (decision D8) so the mobile
// SessionService can be reused. totalCost is always null — Nabla Home has no billing.

public record StatusUpdatedMsg(int StationId, int OcppConnectorId, string Status, bool IsConnected, string? CarId);
public record SessionStartedMsg(int StationId, int SessionId, int TransactionId, decimal? MeterStartWh);
public record SessionStartFailedMsg(int StationId, int SessionId, string Reason);          // Rejected | Timeout
public record MeterUpdatedMsg(int StationId, int SessionId, decimal EnergyKwh, double? CurrentPowerKw, decimal? TotalCost, decimal? Soc);
public record SessionFinalizedMsg(int StationId, int SessionId, decimal EnergyKwh, decimal? TotalCost, string StopReason);
public record SessionStopFailedMsg(int StationId, int SessionId, string Reason);           // Rejected | Timeout

public interface INotifier
{
    Task StatusUpdated(StatusUpdatedMsg m);
    Task SessionStarted(SessionStartedMsg m);
    Task SessionStartFailed(SessionStartFailedMsg m);
    Task MeterUpdated(MeterUpdatedMsg m);
    Task SessionFinalized(SessionFinalizedMsg m);
    Task SessionStopFailed(SessionStopFailedMsg m);
}

public sealed class SignalRNotifier(IHubContext<ChargerHub> hub) : INotifier
{
    private Task Send(int stationId, string method, object payload) =>
        hub.Clients.Group(ChargerHub.Group(stationId)).SendAsync(method, payload);

    public Task StatusUpdated(StatusUpdatedMsg m)           => Send(m.StationId, nameof(StatusUpdated), m);
    public Task SessionStarted(SessionStartedMsg m)         => Send(m.StationId, nameof(SessionStarted), m);
    public Task SessionStartFailed(SessionStartFailedMsg m) => Send(m.StationId, nameof(SessionStartFailed), m);
    public Task MeterUpdated(MeterUpdatedMsg m)             => Send(m.StationId, nameof(MeterUpdated), m);
    public Task SessionFinalized(SessionFinalizedMsg m)     => Send(m.StationId, nameof(SessionFinalized), m);
    public Task SessionStopFailed(SessionStopFailedMsg m)   => Send(m.StationId, nameof(SessionStopFailed), m);
}
