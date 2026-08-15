using FluentAssertions;
using Modules.Commerce.Infrastructure.Services;
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
}
