using System.ComponentModel.DataAnnotations;

namespace EVHomeAPI.DTOs;

public record StationDto(int Id, string OcppId, string Name, string Role, DateTime? ClaimedAt);

public record ClaimStationRequest(
    [Required, StringLength(64, MinimumLength = 1)] string OcppId,
    [Required, StringLength(32, MinimumLength = 1)] string ClaimCode);

// ── Admin ──────────────────────────────────────────────────────────────────────

public record CreateStationRequest(
    [Required, StringLength(64, MinimumLength = 1), RegularExpression("^[A-Za-z0-9_.-]+$")] string OcppId,
    [Required, StringLength(100, MinimumLength = 1)] string Name);

/// <summary>ClaimCode is returned only once; the database keeps just its hash.</summary>
public record CreateStationResponse(int Id, string OcppId, string Name, string ClaimCode);
