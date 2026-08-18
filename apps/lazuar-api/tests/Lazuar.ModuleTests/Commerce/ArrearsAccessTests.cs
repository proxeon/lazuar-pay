using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.Commerce.Application;
using Modules.Commerce.Contracts;
using Modules.Commerce.Domain.Aggregates;
using Modules.One.Contracts;
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
        var one = One("acme", Guid.CreateVersion7());
        var pathId = Guid.CreateVersion7();

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, null, pathId, "acme", one, CancellationToken.None))
            .Should().BeFalse();
        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "", pathId, "acme", one, CancellationToken.None))
            .Should().BeFalse();

        tokens.DidNotReceive().ValidateToken(Arg.Any<string>());
    }

    [Test]
    public async Task MissingSlug_IsUnauthorized()
    {
        var tokens = Substitute.For<IMagicLinkTokenService>();
        var repo = Substitute.For<ICommerceRepository>();
        var one = Substitute.For<IOneQueryService>();

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", Guid.CreateVersion7(), null, one, CancellationToken.None))
            .Should().BeFalse();
        tokens.DidNotReceive().ValidateToken(Arg.Any<string>());
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
        repo.GetSubscriptionByIdForPortalTokenAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
        repo.GetSubscriptionByIdAsync(org, b.Id, Arg.Any<CancellationToken>()).Returns(b);

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", b.Id, "acme", One("acme", org), CancellationToken.None))
            .Should().BeFalse();
    }

    [Test]
    public async Task TokenForA_CanAccessA()
    {
        var a = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.ValidateToken("tok-a").Returns(a.Id);
        var repo = Substitute.For<ICommerceRepository>();
        repo.GetSubscriptionByIdForPortalTokenAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", a.Id, "acme", One("acme", a.OrganizationId), CancellationToken.None))
            .Should().BeTrue();
    }

    [Test]
    public async Task TokenForA_WrongSlug_IsUnauthorized()
    {
        var a = new Subscription(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.ValidateToken("tok-a").Returns(a.Id);
        var repo = Substitute.For<ICommerceRepository>();
        repo.GetSubscriptionByIdForPortalTokenAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", a.Id, "other", One("other", Guid.CreateVersion7()), CancellationToken.None))
            .Should().BeFalse();
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
        repo.GetSubscriptionByIdForPortalTokenAsync(a.Id, Arg.Any<CancellationToken>()).Returns(a);
        repo.GetSubscriptionByIdAsync(org, sibling.Id, Arg.Any<CancellationToken>()).Returns(sibling);

        (await ArrearsAccess.IsAuthorizedAsync(tokens, repo, "tok-a", sibling.Id, "acme", One("acme", org), CancellationToken.None))
            .Should().BeTrue();
    }

    private static IOneQueryService One(string slug, Guid tenantId)
    {
        var one = Substitute.For<IOneQueryService>();
        one.GetTenantIdBySlugAsync(slug).Returns(tenantId);
        return one;
    }
}
