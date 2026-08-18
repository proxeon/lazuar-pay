using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Modules.Communications.Infrastructure;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Communications;

[TestFixture]
public class UnsubscribeJwtSecretTests
{
    [Test]
    public void Empty_Or_Missing_Secret_Fails_Closed()
    {
        Assert.That(PublicComplianceEndpoints.TryJwtHmacSecret(Config(("Jwt:Secret", "")), out var empty), Is.False);
        Assert.That(empty, Is.EqualTo(""));
        Assert.That(PublicComplianceEndpoints.TryJwtHmacSecret(Config(("Jwt:Secret", "   ")), out _), Is.False);
        Assert.That(PublicComplianceEndpoints.TryJwtHmacSecret(new ConfigurationBuilder().Build(), out _), Is.False);
    }

    [Test]
    public void Configured_Secret_Is_Usable()
    {
        Assert.That(
            PublicComplianceEndpoints.TryJwtHmacSecret(Config(("Jwt:Secret", "not-the-dev-fallback")), out var secret),
            Is.True);
        Assert.That(secret, Is.EqualTo("not-the-dev-fallback"));
    }

    private static IConfiguration Config(params (string Key, string Value)[] pairs)
    {
        var dict = new Dictionary<string, string?>();
        foreach (var (key, value) in pairs) dict[key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }
}
