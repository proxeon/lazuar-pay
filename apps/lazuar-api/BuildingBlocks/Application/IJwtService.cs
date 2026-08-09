using System.Security.Claims;

namespace BuildingBlocks.Application;

/// <summary>
/// Thin JWT generation port. Implementation lives in BuildingBlocks.Infrastructure.
/// </summary>
public interface IJwtService
{
    string GenerateToken(IEnumerable<Claim> claims, string secret, string issuer, string audience, int expiryHours);
}
