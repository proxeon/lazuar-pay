using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Modules.One.Domain;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Queries;
using Modules.Payments.Contracts.Queries;
using Modules.Payments.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class GetPaymentsMeTests
{
    private static readonly Guid OrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid KeyId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    [Test]
    public async Task Handle_GoodKey_ReturnsWorkspaceScopesAndNoSecret()
    {
        var repo = Substitute.For<ITenantPaymentConfigRepository>();
        repo.GetAllByTenantIdAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration>
            {
                new(OrgId, "STRIPE", "enc-api-key-not-plaintext", "enc-whsec", "merch_1", isActive: true)
            });

        var handler = new GetPaymentsMeQueryHandler(repo);
        var scopes = new[]
        {
            PlatformApiScopes.PaymentsCheckoutsWrite,
            PlatformApiScopes.PaymentsCheckoutsRead,
            PlatformApiScopes.WebhooksEndpointsManage
        };

        var result = await handler.Handle(
            new GetPaymentsMeQuery(OrgId, KeyId, IsTestMode: true, scopes, "AuraBook guest"),
            CancellationToken.None);

        result.WorkspaceId.Should().Be(OrgId);
        result.OrganizationId.Should().Be(OrgId);
        result.KeyId.Should().Be(KeyId);
        result.IsTestMode.Should().BeTrue();
        result.KeyName.Should().Be("AuraBook guest");
        result.Scopes.Should().BeEquivalentTo(scopes);
        result.HasActiveGateway.Should().BeTrue();
        result.GatewayNames.Should().Equal("STRIPE");

        var json = JsonSerializer.Serialize(result);
        json.Should().NotContain("sk_");
        json.Should().NotContain("whsec_");
        json.Should().NotContain("api_key");
        json.Should().NotContain("merch_1");
        json.Should().NotContain("enc-api-key");
    }

    [Test]
    public async Task Handle_TestKey_IsTestModeTrue()
    {
        var repo = EmptyRepo();
        var result = await new GetPaymentsMeQueryHandler(repo).Handle(
            new GetPaymentsMeQuery(OrgId, KeyId, IsTestMode: true, [PlatformApiScopes.PaymentsCheckoutsRead], "t"),
            CancellationToken.None);

        result.IsTestMode.Should().BeTrue();
    }

    [Test]
    public async Task Handle_LiveKey_IsTestModeFalse()
    {
        var repo = EmptyRepo();
        var result = await new GetPaymentsMeQueryHandler(repo).Handle(
            new GetPaymentsMeQuery(OrgId, KeyId, IsTestMode: false, [PlatformApiScopes.PaymentsCheckoutsWrite], "l"),
            CancellationToken.None);

        result.IsTestMode.Should().BeFalse();
    }

    [Test]
    public async Task Handle_NoActiveGateway_HasActiveGatewayFalse()
    {
        var repo = Substitute.For<ITenantPaymentConfigRepository>();
        repo.GetAllByTenantIdAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration>
            {
                new(OrgId, "STRIPE", "enc", null, null, isActive: false)
            });

        var result = await new GetPaymentsMeQueryHandler(repo).Handle(
            new GetPaymentsMeQuery(OrgId, KeyId, true, [PlatformApiScopes.PaymentsCheckoutsRead], null),
            CancellationToken.None);

        result.HasActiveGateway.Should().BeFalse();
        result.GatewayNames.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ActiveGateway_ListsGatewayName()
    {
        var repo = Substitute.For<ITenantPaymentConfigRepository>();
        repo.GetAllByTenantIdAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration>
            {
                new(OrgId, "stripe", "enc-cipher-not-merchant", null, "acct_secret", isActive: true)
            });

        var result = await new GetPaymentsMeQueryHandler(repo).Handle(
            new GetPaymentsMeQuery(OrgId, KeyId, true, [PlatformApiScopes.PaymentsConfigRead], "n"),
            CancellationToken.None);

        result.HasActiveGateway.Should().BeTrue();
        result.GatewayNames.Should().Equal("STRIPE");
        JsonSerializer.Serialize(result).Should().NotContain("acct_secret");
        JsonSerializer.Serialize(result).Should().NotContain("enc-cipher");
    }

    private static ITenantPaymentConfigRepository EmptyRepo()
    {
        var repo = Substitute.For<ITenantPaymentConfigRepository>();
        repo.GetAllByTenantIdAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration>());
        return repo;
    }
}
