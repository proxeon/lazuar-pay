using System;
using System.Text;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Modules.Commerce.Infrastructure.Security;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class MagicLinkTokenServiceTests
{
    private const string Secret = "test-jwt-secret-for-magic-link-parity";

    private static MagicLinkTokenService CreateSut(string? secret = Secret)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = secret
            })
            .Build();
        return new MagicLinkTokenService(config);
    }

    [Test]
    public void GenerateToken_ThenValidateToken_ReturnsSameSubscriptionId()
    {
        var subscriptionId = Guid.CreateVersion7();
        var sut = CreateSut();

        var token = sut.GenerateToken(subscriptionId);
        var validated = sut.ValidateToken(token);

        validated.Should().Be(subscriptionId);
    }

    [Test]
    public void GenerateToken_WireFormat_IsBase64OfGuidExpiryHmacHex()
    {
        var subscriptionId = Guid.CreateVersion7();
        var sut = CreateSut();

        var token = sut.GenerateToken(subscriptionId);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        var parts = decoded.Split(':');

        parts.Should().HaveCount(3);
        parts[0].Should().Be(subscriptionId.ToString());
        long.TryParse(parts[1], out var expiry).Should().BeTrue();
        expiry.Should().BeGreaterThan(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        // 24h window (allow small clock skew)
        expiry.Should().BeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddHours(24).AddMinutes(1).ToUnixTimeSeconds());
        parts[2].Should().MatchRegex("^[0-9a-f]{64}$");
    }

    [Test]
    public void ValidateToken_TamperedPayload_ReturnsNull()
    {
        var subscriptionId = Guid.CreateVersion7();
        var sut = CreateSut();
        var token = sut.GenerateToken(subscriptionId);

        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(token));
        var parts = decoded.Split(':');
        parts[0] = Guid.CreateVersion7().ToString();
        var tampered = Convert.ToBase64String(Encoding.UTF8.GetBytes(string.Join(':', parts)));

        sut.ValidateToken(tampered).Should().BeNull();
    }

    [Test]
    public void ValidateToken_WrongSecret_ReturnsNull()
    {
        var subscriptionId = Guid.CreateVersion7();
        var mint = CreateSut("secret-a");
        var validate = CreateSut("secret-b");

        validate.ValidateToken(mint.GenerateToken(subscriptionId)).Should().BeNull();
    }

    [Test]
    public void ValidateToken_Garbage_ReturnsNull()
    {
        var sut = CreateSut();
        sut.ValidateToken("not-a-token").Should().BeNull();
        sut.ValidateToken("").Should().BeNull();
    }

    [Test]
    public void Constructor_MissingJwtSecret_Throws()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var act = () => new MagicLinkTokenService(config);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Jwt:Secret*");
    }

    [Test]
    public void ValidateToken_Expired_ReturnsNull()
    {
        var subscriptionId = Guid.CreateVersion7();
        var sut = CreateSut();
        var expiry = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
        var payload = $"{subscriptionId}:{expiry}";
        var hash = Convert.ToHexString(System.Security.Cryptography.HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret),
            Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{payload}:{hash}"));

        sut.ValidateToken(token).Should().BeNull();
    }
}
