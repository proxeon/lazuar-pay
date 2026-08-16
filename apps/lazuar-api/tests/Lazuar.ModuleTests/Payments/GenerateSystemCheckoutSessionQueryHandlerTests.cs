using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Queries;
using Modules.Payments.Contracts;
using Modules.Payments.Contracts.Queries;
using Modules.Payments.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class GenerateSystemCheckoutSessionQueryHandlerTests
{
    private static readonly Guid SystemId = PlatformCheckoutTypes.SystemOrganizationId;
    private static readonly Guid PayingTenant = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private ITenantPaymentConfigRepository _repo = null!;
    private IPaymentGatewayFactory _factory = null!;
    private IPaymentGatewayAdapter _adapter = null!;
    private ISecretVault _vault = null!;
    private GenerateSystemCheckoutSessionQueryHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _repo = Substitute.For<ITenantPaymentConfigRepository>();
        _factory = Substitute.For<IPaymentGatewayFactory>();
        _adapter = Substitute.For<IPaymentGatewayAdapter>();
        _vault = Substitute.For<ISecretVault>();
        _vault.Decrypt(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
        _factory.GetAdapter(Arg.Any<string>()).Returns(_adapter);
        _handler = new GenerateSystemCheckoutSessionQueryHandler(_factory, _repo, _vault);
    }

    private void WithStripeOnly()
    {
        var stripe = new TenantPaymentConfiguration(SystemId, "STRIPE", "sk_test", "whsec", null, isActive: true);
        _repo.GetAllByTenantIdAsync(SystemId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration> { stripe });
        _factory.GetAdapter("STRIPE").Returns(_adapter);
        _adapter.GatewayType.Returns("STRIPE");
        _adapter.GenerateCheckoutAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>())
            .Returns(new GatewayCheckoutResult(true, "https://stripe.test/cs", "cs_1", null));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Handle_MissingOrBlankType_Throws_AndDoesNotDefaultToCredits(string? type)
    {
        WithStripeOnly();
        var metadata = new Dictionary<string, string> { ["tenant_id"] = PayingTenant.ToString() };
        if (type != null)
            metadata["type"] = type;

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => _handler.Handle(
            new GenerateSystemCheckoutSessionQuery(
                PayingTenant,
                99m,
                "MYR",
                "Hub Starter (monthly)",
                "ada@example.com",
                "https://ok",
                "https://ok",
                metadata),
            CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("type").IgnoreCase);
        Assert.That(
            metadata.GetValueOrDefault("type"),
            Is.Not.EqualTo(PlatformCheckoutTypes.UtilityCreditTopup));
        await _adapter.DidNotReceive().GenerateCheckoutAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [Test]
    public async Task Handle_SaasType_IsNotRewrittenToCredits_AndUsesFirstActiveGateway()
    {
        WithStripeOnly();
        Dictionary<string, string>? sent = null;
        _adapter.GenerateCheckoutAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Do<Dictionary<string, string>>(m => sent = m), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>())
            .Returns(new GatewayCheckoutResult(true, "https://stripe.test/cs", "cs_1", null));

        var url = await _handler.Handle(new GenerateSystemCheckoutSessionQuery(
            PayingTenant,
            99m,
            "MYR",
            "Hub Starter (monthly)",
            "ada@example.com",
            "https://ok",
            "https://ok",
            new Dictionary<string, string>
            {
                ["type"] = PlatformCheckoutTypes.PlatformSaasFee,
                ["tenant_id"] = PayingTenant.ToString()
            }), CancellationToken.None);

        url.Should().Be("https://stripe.test/cs");
        sent.Should().NotBeNull();
        sent!["type"].Should().Be(PlatformCheckoutTypes.PlatformSaasFee);
        sent["tenant_id"].Should().Be(PayingTenant.ToString());
        sent.Should().NotContainValue(PlatformCheckoutTypes.UtilityCreditTopup);
        await _adapter.Received(1).GenerateCheckoutAsync(
            "sk_test",
            SystemId,
            99m,
            "MYR",
            "Hub Starter (monthly)",
            "ada@example.com",
            "https://ok",
            "https://ok",
            Arg.Any<Dictionary<string, string>>(),
            null,
            false,
            1);
    }
}
