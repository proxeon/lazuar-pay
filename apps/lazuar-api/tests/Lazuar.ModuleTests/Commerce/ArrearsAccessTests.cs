using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts;
using Modules.Commerce.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

[TestFixture]
public class ArrearsAccessTests
{
    [Test]
    public async Task NoToken_IsUnauthorized()
    {
        var tokens = Substitute.For<IMagicLinkTokenService>();
        var repo = Substitute.For<ICommerceRepository>();
        var pathId = Guid.CreateVersion7();

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, null, pathId, CancellationToken.None))
            .Should().BeFalse();
        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "", pathId, CancellationToken.None))
            .Should().BeFalse();
        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "   ", pathId, CancellationToken.None))
            .Should().BeFalse();

        tokens.DidNotReceive().ValidateToken(Arg.Any<string>());
        await repo.DidNotReceive().GetSubscriptionByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TokenForA_CannotAccessB_WhenDifferentClient()
    {
        var org = Guid.CreateVersion7();
        var a = new Subscription(org, Guid.CreateVersion7(), Guid.CreateVersion7());
        var b = new Subscription(org, Guid.CreateVersion7(), Guid.CreateVersion7());

        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.ValidateToken("tok-a").Returns(a.Id);

        var repo = Substitute.For<ICommerceRepository>();
        repo.GetSubscriptionByIdAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
        repo.GetSubscriptionByIdAsync(b.Id, Arg.Any<CancellationToken>()).Returns(b);

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", b.Id, CancellationToken.None))
            .Should().BeFalse();
    }

    [Test]
    public async Task TokenForA_CanAccessA()
    {
        var a = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.ValidateToken("tok-a").Returns(a.Id);
        var repo = Substitute.For<ICommerceRepository>();

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", a.Id, CancellationToken.None))
            .Should().BeTrue();

        await repo.DidNotReceive().GetSubscriptionByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task TokenForA_CanAccessSibling_SameOrganizationAndClient()
    {
        var org = Guid.CreateVersion7();
        var client = Guid.CreateVersion7();
        var a = new Subscription(org, client, Guid.CreateVersion7());
        var sibling = new Subscription(org, client, Guid.CreateVersion7());

        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.ValidateToken("tok-a").Returns(a.Id);

        var repo = Substitute.For<ICommerceRepository>();
        repo.GetSubscriptionByIdAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
        repo.GetSubscriptionByIdAsync(sibling.Id, Arg.Any<CancellationToken>()).Returns(sibling);

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", sibling.Id, CancellationToken.None))
            .Should().BeTrue();
    }
}
