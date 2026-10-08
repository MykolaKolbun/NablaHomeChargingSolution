using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EVHomeAPI.Models;
using Microsoft.IdentityModel.Tokens;

namespace EVHomeAPI.Services;

public class TokenService(IConfiguration config)
{
    public string CreateToken(User user, TimeSpan? ttl = null)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Name,           user.Name),
        };

        var token = new JwtSecurityToken(
            issuer:             config["Jwt:Issuer"],
            audience:           config["Jwt:Audience"],
            claims:             claims,
            expires:            DateTime.UtcNow.Add(ttl ?? TimeSpan.FromDays(30)),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
