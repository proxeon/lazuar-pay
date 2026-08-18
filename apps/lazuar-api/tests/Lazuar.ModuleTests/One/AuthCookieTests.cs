using FluentAssertions;
using Modules.One.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class AuthCookieTests
{
    [Test]
    public void ProductionDelete_MatchesSetDomainAndFlags()
    {
        var set = AuthCookie.MerchantOptions(isDev: false);
        var admin = AuthCookie.AdminOptions(isDev: false);

        set.Domain.Should().Be(".lazuar.com");
        set.Secure.Should().BeTrue();
        set.HttpOnly.Should().BeTrue();
        set.SameSite.Should().Be(Microsoft.AspNetCore.Http.SameSiteMode.Lax);

        admin.Domain.Should().Be(".lazuar.com");
        admin.Path.Should().Be("/api/v1/platform");
    }

    [Test]
    public void Development_OmitsDomain()
    {
        AuthCookie.MerchantOptions(isDev: true).Domain.Should().BeNull();
        AuthCookie.AdminOptions(isDev: true).Path.Should().Be("/api/v1/platform");
    }
}
