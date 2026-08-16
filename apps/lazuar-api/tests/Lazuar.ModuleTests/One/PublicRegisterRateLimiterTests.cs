using System.Threading.Tasks;
using Modules.One.Infrastructure.Services;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class PublicRegisterRateLimiterTests
{
    [Test]
    public async Task Blocks_After_Budget()
    {
        var limiter = new PublicRegisterRateLimiter();
        for (var i = 0; i < PublicRegisterRateLimiter.Limit; i++)
        {
            Assert.That(await limiter.TryAcquireAsync("email:a@b.co|ip:1.1.1.1"), Is.True);
        }

        Assert.That(await limiter.TryAcquireAsync("email:a@b.co|ip:1.1.1.1"), Is.False);
        Assert.That(await limiter.TryAcquireAsync("email:other@b.co|ip:9.9.9.9"), Is.True);
    }
}
