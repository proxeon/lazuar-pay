using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.Commerce.Application.Queries;
using Modules.Commerce.Contracts;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Services;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// LP-024 — public checkout status poller contract.
/// Mapping is the SSoT used by <c>GetCheckoutStatusAsync</c>; SQL org-binding is covered in IntegrationTests.
/// </summary>
[TestFixture]
public class GetCheckoutStatusTests
{
    [Test]
    public void MapPublicCheckoutStatus_MissingSession_IsNull()
    {
        CommerceQueryService.MapPublicCheckoutStatus(null).Should().BeNull();
    }

    [Test]
    public void MapPublicCheckoutStatus_Open_IsPending_WithNullToken()
    {
        var dto = CommerceQueryService.MapPublicCheckoutStatus("OPEN");
        dto.Should().NotBeNull();
        dto!.Status.Should().Be("PENDING");
        dto.Token.Should().BeNull();
    }

    [Test]
    public void MapPublicCheckoutStatus_Completed_IsCompleted_WithNullToken()
    {
        var dto = CommerceQueryService.MapPublicCheckoutStatus("COMPLETED");
        dto.Should().NotBeNull();
        dto!.Status.Should().Be("COMPLETED");
        dto.Token.Should().BeNull();
    }

    [Test]
    public void MapPublicCheckoutStatus_Expired_IsExpired_NeverCompleted()
    {
        var dto = CommerceQueryService.MapPublicCheckoutStatus("EXPIRED");
        dto.Should().NotBeNull();
        dto!.Status.Should().Be("EXPIRED");
        dto.Status.Should().NotBe("COMPLETED");
        dto.Token.Should().BeNull();
    }

    [Test]
    public void MapPublicCheckoutStatus_Active_IsPending_NeverCompleted()
    {
        var dto = CommerceQueryService.MapPublicCheckoutStatus("ACTIVE");
        dto.Should().NotBeNull();
        dto!.Status.Should().Be("PENDING");
        dto.Status.Should().NotBe("COMPLETED");
        dto.Token.Should().BeNull();
    }

    [Test]
    public async Task MintPortalTokenIfCompleted_CompletedWithSubscription_ReturnsToken()
    {
        var orgId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var subId = Guid.CreateVersion7();
        var query = Substitute.For<ICommerceQueryService>();
        query.FindSubscriptionIdForCheckoutSessionAsync(orgId, sessionId, Arg.Any<CancellationToken>())
            .Returns(subId);
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.GenerateToken(subId).Returns("portal-token");

        var minted = await PublicCheckoutEndpoints.MintPortalTokenIfCompletedAsync(
            "COMPLETED", orgId, sessionId, query, tokens);

        minted.Should().Be("portal-token");
    }

    [Test]
    public async Task MintPortalTokenIfCompleted_CompletedWithoutSubscription_MintsProfileToken()
    {
        var orgId = Guid.CreateVersion7();
        var sessionId = Guid.CreateVersion7();
        var profileId = Guid.CreateVersion7();
        var query = Substitute.For<ICommerceQueryService>();
        query.FindSubscriptionIdForCheckoutSessionAsync(orgId, sessionId, Arg.Any<CancellationToken>())
            .Returns((Guid?)null);
        query.FindClientProfileIdForCheckoutSessionAsync(orgId, sessionId, Arg.Any<CancellationToken>())
            .Returns(profileId);
        var tokens = Substitute.For<IMagicLinkTokenService>();
        tokens.GenerateToken(profileId).Returns("profile-portal-token");

        var minted = await PublicCheckoutEndpoints.MintPortalTokenIfCompletedAsync(
            "COMPLETED", orgId, sessionId, query, tokens);

        minted.Should().Be("profile-portal-token");
        tokens.Received(1).GenerateToken(profileId);
    }

    [Test]
    public async Task MintPortalTokenIfCompleted_Pending_IsNull()
    {
        var minted = await PublicCheckoutEndpoints.MintPortalTokenIfCompletedAsync(
            "PENDING",
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Substitute.For<ICommerceQueryService>(),
            Substitute.For<IMagicLinkTokenService>());

        minted.Should().BeNull();
    }
}
