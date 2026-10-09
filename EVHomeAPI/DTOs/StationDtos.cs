using System.ComponentModel.DataAnnotations;
using EVHomeAPI.Models;

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
    string?   LimitStatus)
{
    public static StationDto From(Station s, StationRole role) => new(
        s.Id, s.OcppId, s.Name, role.ToString(), s.ClaimedAt, s.IsOnline, s.ConnectorStatus, s.LastStatusAt,
        s.MaxCurrentA, s.CurrentLimitA, s.LimitStatus);
}

/// <summary>limitA null = remove the limit. Range 6…MaxCurrentA, step 0.1.</summary>
public record SetLimitRequest(decimal? LimitA);

public record ClaimStationRequest(
    [Required, StringLength(64, MinimumLength = 1)] string OcppId,
    [Required, StringLength(32, MinimumLength = 1)] string ClaimCode);

// ── Admin ──────────────────────────────────────────────────────────────────────

public record CreateStationRequest(
    [Required, StringLength(64, MinimumLength = 1), RegularExpression("^[A-Za-z0-9_.-]+$")] string OcppId,
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Range(6, 80)] int MaxCurrentA = 32);

/// <summary>ClaimCode is returned only once; the database keeps just its hash.</summary>
public record CreateStationResponse(int Id, string OcppId, string Name, string ClaimCode);
