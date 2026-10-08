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
    DateTime? LastStatusAt)
{
    public static StationDto From(Station s, StationRole role) => new(
        s.Id, s.OcppId, s.Name, role.ToString(), s.ClaimedAt, s.IsOnline, s.ConnectorStatus, s.LastStatusAt);
}

public record ClaimStationRequest(
    [Required, StringLength(64, MinimumLength = 1)] string OcppId,
    [Required, StringLength(32, MinimumLength = 1)] string ClaimCode);

// ── Admin ──────────────────────────────────────────────────────────────────────

public record CreateStationRequest(
    [Required, StringLength(64, MinimumLength = 1), RegularExpression("^[A-Za-z0-9_.-]+$")] string OcppId,
    [Required, StringLength(100, MinimumLength = 1)] string Name);

/// <summary>ClaimCode is returned only once; the database keeps just its hash.</summary>
public record CreateStationResponse(int Id, string OcppId, string Name, string ClaimCode);
