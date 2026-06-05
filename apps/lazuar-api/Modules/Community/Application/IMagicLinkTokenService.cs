namespace Modules.Community.Application;

/// <summary>
/// Domain-agnostic abstraction for cryptographic token generation.
/// Implemented in Infrastructure using HMACSHA256 and configuration secrets.
/// </summary>
public interface IMagicLinkTokenService
{
    string GenerateToken(Guid subscriptionId);
    Guid? ValidateToken(string token);
}
