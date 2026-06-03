using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace BuildingBlocks.Infrastructure;

public interface IJwtService 
{ 
    string GenerateToken(IEnumerable<Claim> claims, string secret, string issuer, string audience, int expiryHours); 
}

public class JwtService : IJwtService
{
    public string GenerateToken(IEnumerable<Claim> claims, string secret, string issuer, string audience, int expiryHours)
    {
        var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(expiryHours),
            signingCredentials: creds);
            
        return new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(token);
    }
}
