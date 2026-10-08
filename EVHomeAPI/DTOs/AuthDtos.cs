using System.ComponentModel.DataAnnotations;

namespace EVHomeAPI.DTOs;

// Shapes mirror the commercial Nabla app (minus wallet) so the mobile auth flow can be reused.

public record RegisterRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, EmailAddress, StringLength(256)]       string Email,
    [Required, StringLength(128, MinimumLength = 8)] string Password);

public record LoginRequest(
    [Required] string Email,
    [Required] string Password);

public record AuthResponse(string Token, string Name, string Email);

public record ProfileResponse(string Name, string Email);

public record UpdateProfileRequest(
    [Required, StringLength(100, MinimumLength = 1)] string Name,
    [Required, EmailAddress, StringLength(256)]       string Email,
    [StringLength(128, MinimumLength = 8)]            string? NewPassword,
    string? CurrentPassword);
