using System.Threading.Tasks;
using FluentAssertions;
using Modules.Commerce.Infrastructure.Security;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class PortalMagicLinkRateLimiterTests
{
    [Test]
    public async Task BlocksAfterBudget()
    {
        var limiter = new PortalMagicLinkRateLimiter();
        for (var i = 0; i < PortalMagicLinkRateLimiter.Limit; i++)
        {
            (await limiter.TryAcquireAsync("ip:1.1.1.1")).Should().BeTrue();
        }

        (await limiter.TryAcquireAsync("ip:1.1.1.1")).Should().BeFalse();
        (await limiter.TryAcquireAsync("ip:2.2.2.2")).Should().BeTrue();
    }
}
