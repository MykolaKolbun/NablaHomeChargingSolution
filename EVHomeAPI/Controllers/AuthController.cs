using EVHomeAPI.Data;
using EVHomeAPI.DTOs;
using EVHomeAPI.Models;
using EVHomeAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace EVHomeAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest req)
    {
        var email = NormalizeEmail(req.Email);
        if (await db.Users.AnyAsync(u => u.Email == email))
            return Conflict("Email already in use.");

        var user = new User
        {
            Name         = req.Name.Trim(),
            Email        = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        return Ok(new AuthResponse(tokens.CreateToken(user), user.Name, user.Email));
    }

    [HttpPost("login")]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest req)
    {
        var email = NormalizeEmail(req.Email);
        var user  = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user is null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
            return Unauthorized("Invalid email or password.");

        return Ok(new AuthResponse(tokens.CreateToken(user), user.Name, user.Email));
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<ActionResult<ProfileResponse>> GetProfile()
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user is null) return NotFound();
        return Ok(new ProfileResponse(user.Name, user.Email));
    }

    [Authorize]
    [HttpPut("profile")]
    public async Task<ActionResult<ProfileResponse>> UpdateProfile(UpdateProfileRequest req)
    {
        var user = await db.Users.FindAsync(User.GetUserId());
        if (user is null) return NotFound();

        var email = NormalizeEmail(req.Email);
        if (email != user.Email)
        {
            if (await db.Users.AnyAsync(u => u.Email == email && u.Id != user.Id))
                return Conflict("Email is already in use.");
            user.Email = email;
        }

        if (!string.IsNullOrEmpty(req.NewPassword))
        {
            if (string.IsNullOrEmpty(req.CurrentPassword) ||
                !BCrypt.Net.BCrypt.Verify(req.CurrentPassword, user.PasswordHash))
                return BadRequest("Current password is incorrect.");
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.NewPassword);
        }

        user.Name = req.Name.Trim();
        await db.SaveChangesAsync();
        return Ok(new ProfileResponse(user.Name, user.Email));
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
