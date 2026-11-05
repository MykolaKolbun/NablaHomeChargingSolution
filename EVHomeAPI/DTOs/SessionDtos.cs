using EVHomeAPI.Models;

namespace EVHomeAPI.DTOs;

public record SessionDto(
    int       Id,
    int       StationId,
    string    Status,          // Pending | Active | Stopping | Completed | Cancelled
    string    InitiatedBy,     // App | Charger
    string?   StopReason,      // UserInitiated | ChargerInitiated
    DateTime  CreatedAt,
    DateTime? StartedAt,
    DateTime? EndedAt,
    decimal   EnergyKwh,
    double?   CurrentPowerKw,
    decimal?  Soc,
    int?      TransactionId,
    int       ConnectorId)
{
    public static SessionDto From(ChargingSession s) => new(
        s.Id, s.StationId, s.Status.ToString(), s.InitiatedBy.ToString(), s.StopReason?.ToString(),
        s.CreatedAt, s.StartedAt, s.EndedAt, s.EnergyKwh, s.CurrentPowerKw, s.Soc, s.OcppTransactionId,
        s.ConnectorId);
}

/// <summary>Same shape as Nabla's GET /api/sessions/{id}/meter-history.</summary>
public record MeterPointDto(int ElapsedSec, double? CurrentPowerKw, decimal? Soc);

public record StartChargingRequest([System.ComponentModel.DataAnnotations.Range(1, 16)] int ConnectorId = 1);
