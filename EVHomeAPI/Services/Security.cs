using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EVHomeAPI.Services;

public static class ClaimsPrincipalExtensions
{
    /// <summary>User id from the JWT. Only call on [Authorize] endpoints.</summary>
    public static int GetUserId(this ClaimsPrincipal user) =>
        int.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public static class RateLimitPolicies
{
    public const string Auth  = "auth";    // login / register
    public const string Claim = "claim";   // station claim (brute-force target)

    /// <summary>
    /// Real client IP. Behind Cloudflare Tunnel every request arrives from cloudflared on
    /// localhost, so RemoteIpAddress alone would put all users in one rate-limit bucket.
    /// CF-Connecting-IP is set by Cloudflare's edge; the API port is not exposed elsewhere.
    /// </summary>
    public static string ClientKey(HttpContext ctx) =>
        ctx.Request.Headers["CF-Connecting-IP"].FirstOrDefault()
        ?? ctx.Connection.RemoteIpAddress?.ToString()
        ?? "unknown";
}

/// <summary>Requires header X-Admin-Key equal to config AdminKey (constant-time compare).</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AdminKeyAttribute : Attribute, IAuthorizationFilter
{
    public const string HeaderName = "X-Admin-Key";

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var expected = context.HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()["AdminKey"];
        var provided = context.HttpContext.Request.Headers[HeaderName].FirstOrDefault();

        if (string.IsNullOrEmpty(expected) || string.IsNullOrEmpty(provided) ||
            !CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(provided)))
        {
            context.Result = new UnauthorizedResult();
        }
    }
}
