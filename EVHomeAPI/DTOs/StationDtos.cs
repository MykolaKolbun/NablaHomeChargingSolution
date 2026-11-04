using System.ComponentModel.DataAnnotations;
using EVHomeAPI.Models;
using EVHomeAPI.Services;

namespace EVHomeAPI.DTOs;

public record StationDto(
    int       Id,
    string    OcppId,
    string    Name,
    string    Role,
    DateTime? ClaimedAt,
    bool      IsOnline,
    string?   ConnectorStatus,
    DateTime? LastStatusAt,
    int       MaxCurrentA,
    decimal?  CurrentLimitA,
    string?   LimitStatus,
    bool      ManageLimit)
{
    public static StationDto From(Station s, StationRole role) => new(
        s.Id, s.OcppId, s.Name, role.ToString(), s.ClaimedAt, s.IsOnline, s.ConnectorStatus, s.LastStatusAt,
        s.MaxCurrentA, s.CurrentLimitA, s.LimitStatus, s.ManageLimit);
}

/// <summary>limitA null = at the installation maximum (MaxCurrentA), never unlimited. Range 6…MaxCurrentA, step 0.1.</summary>
public record SetLimitRequest(decimal? LimitA);

public record ClaimStationRequest(
    [Required, StringLength(64, MinimumLength = 1)] string OcppId,
    [Required, StringLength(32, MinimumLength = 1)] string ClaimCode);

// ── Admin ──────────────────────────────────────────────────────────────────────

public record CreateStationRequest(
    [Required, StringLength(64, MinimumLength = 1), RegularExpression(OcppIds.Pattern)] string OcppId,
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Range(6, 80)] int MaxCurrentA = 32,
    bool ManageLimit = true);

public record SetMaxCurrentRequest([Range(6, 80)] int MaxCurrentA);

/// <summary>false = EVHomeAPI never sends charging profiles to this charger.</summary>
public record SetLimitManagementRequest(bool ManageLimit);

/// <summary>ClaimCode is returned only once; the database keeps just its hash.</summary>
public record CreateStationResponse(int Id, string OcppId, string Name, string ClaimCode);
