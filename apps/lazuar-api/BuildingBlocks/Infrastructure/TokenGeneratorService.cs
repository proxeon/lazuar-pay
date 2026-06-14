using System.Security.Cryptography;
using System.Text;
using BuildingBlocks.Application;

namespace BuildingBlocks.Infrastructure;

public class TokenGeneratorService : ITokenGeneratorService
{
    public GeneratedToken GenerateSecureToken(int length = 32)
    {
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        var plainToken = Convert.ToBase64String(bytes)
            .Replace("+", "-").Replace("/", "_").Replace("=", "");

        var hash = HashToken(plainToken);

        return new GeneratedToken(plainToken, hash);
    }

    public string HashToken(string plainToken)
    {
        var bytes = Encoding.UTF8.GetBytes(plainToken);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
