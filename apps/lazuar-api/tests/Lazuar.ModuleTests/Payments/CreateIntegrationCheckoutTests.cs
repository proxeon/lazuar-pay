using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Modules.Payments.Application.Commands;
using Modules.Payments.Application.Exceptions;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Queries;
using Modules.Payments.Application.Services;
using Modules.Payments.Contracts.Commands;
using Modules.Payments.Contracts.Queries;
using Modules.Payments.Domain.Aggregates;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class CreateIntegrationCheckoutTests
{
    private static readonly Guid OrgId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherOrgId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private ITenantPaymentConfigRepository _configRepo = null!;
    private IPaymentGatewayFactory _factory = null!;
    private IPaymentGatewayAdapter _adapter = null!;
    private ISecretVault _vault = null!;
    private InMemorySessionRepo _sessions = null!;
    private CheckoutSessionCashier _cashier = null!;
    private CreateIntegrationCheckoutCommandHandler _create = null!;
    private GetIntegrationCheckoutQueryHandler _get = null!;

    [SetUp]
    public void SetUp()
    {
        _configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        _factory = Substitute.For<IPaymentGatewayFactory>();
        _adapter = Substitute.For<IPaymentGatewayAdapter>();
        _vault = Substitute.For<ISecretVault>();
        _sessions = new InMemorySessionRepo();

        // DecryptOrPlaintext is an extension — mock Decrypt (throws → extension returns plaintext).
        _vault.Decrypt(Arg.Any<string>()).Returns(ci => ci.ArgAt<string>(0));
        _factory.GetAdapter(Arg.Any<string>()).Returns(_adapter);
        _adapter.GatewayType.Returns("STRIPE");

        _cashier = new CheckoutSessionCashier(_configRepo, _factory, _vault);
        _create = new CreateIntegrationCheckoutCommandHandler(_sessions, _cashier);
        _get = new GetIntegrationCheckoutQueryHandler(_sessions);
    }

    private void WithActiveGateway(string gatewayType, string apiKey = "sk_test")
    {
        var config = new TenantPaymentConfiguration(OrgId, gatewayType, apiKey, "whsec", "merchant", isActive: true);
        _configRepo.GetByTenantAndGatewayAsync(OrgId, gatewayType, Arg.Any<CancellationToken>())
            .Returns(config);
        _configRepo.GetAllByTenantIdAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration> { config });
        _factory.GetAdapter(gatewayType).Returns(_adapter);
        _adapter.GatewayType.Returns(gatewayType);
    }

    private void AdapterReturns(string url, string sessionId)
    {
        _adapter.GenerateCheckoutAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>())
            .Returns(new GatewayCheckoutResult(true, url, sessionId, null));
    }

    private static CreateIntegrationCheckoutCommand ValidCommand(
        string? idempotencyKey = "aura:booking:1:deposit:5000",
        string? gatewayName = null,
        decimal amount = 50m,
        Dictionary<string, string>? metadata = null) =>
        new(
            OrganizationId: OrgId,
            Amount: amount,
            Currency: "MYR",
            Description: "Booking deposit #1",
            CustomerEmail: "guest@example.com",
            SuccessUrl: "https://app.aura.example/ok",
            CancelUrl: "https://app.aura.example/cancel",
            CustomerName: "Aina",
            GatewayName: gatewayName,
            SetupFutureUsage: false,
            IdempotencyKey: idempotencyKey,
            Metadata: metadata ?? new Dictionary<string, string>
            {
                ["integrator"] = "aura",
                ["type"] = "booking_payment",
                ["booking_id"] = "b-1",
                ["payment_type"] = "deposit"
            });

    [Test]
    public async Task Create_TestKey_With_StripeLiveK2_Throws_KeyModeMismatch()
    {
        WithActiveGateway("STRIPE", "sk_live_real_stripe_secret");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs_live", "cs_live");

        var act = async () => await _create.Handle(
            ValidCommand() with { RequestIsTestMode = true },
            CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.KeyModeMismatch);
        await _adapter.DidNotReceive().GenerateCheckoutAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [Test]
    public async Task Create_TestKey_With_BillplzPlainK2_DoesNotThrow()
    {
        WithActiveGateway("BILLPLZ", "billplz_collection_secret");
        AdapterReturns("https://www.billplz.com/bills/bill_x", "bill_x");

        var result = await _create.Handle(
            ValidCommand(gatewayName: "BILLPLZ") with { RequestIsTestMode = true },
            CancellationToken.None);

        result.Gateway.Should().Be("BILLPLZ");
        result.ProviderSessionId.Should().Be("bill_x");
    }

    [Test]
    public async Task Create_WithMockedStripe_PersistsSession_AndStampsMetadata()
    {
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs_test_x", "cs_test_x");

        var result = await _create.Handle(ValidCommand(gatewayName: "STRIPE"), CancellationToken.None);

        result.CheckoutUrl.Should().Be("https://checkout.stripe.com/c/pay/cs_test_x");
        result.Gateway.Should().Be("STRIPE");
        result.Status.Should().Be("open");
        result.ProviderSessionId.Should().Be("cs_test_x");
        result.Metadata.Should().ContainKey("hub_workspace_id").WhoseValue.Should().Be(OrgId.ToString());
        result.Metadata.Should().ContainKey("checkout_id").WhoseValue.Should().Be(result.CheckoutId.ToString());
        result.Metadata.Should().ContainKey("tenant_id");
        result.Metadata["integrator"].Should().Be("aura");
        result.Metadata["booking_id"].Should().Be("b-1");
        result.Metadata["type"].Should().Be("booking_payment");

        _sessions.Items.Should().HaveCount(1);
        _sessions.Items[0].ProviderSessionId.Should().Be("cs_test_x");
        _sessions.Items[0].MetadataJson.Should().Contain("booking_id");
    }

    [Test]
    public async Task Create_IdempotentReplay_SameKey_SingleAdapterCall()
    {
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs_test_y", "cs_test_y");

        var cmd = ValidCommand(idempotencyKey: "key-1");
        var first = await _create.Handle(cmd, CancellationToken.None);
        var second = await _create.Handle(cmd, CancellationToken.None);

        second.CheckoutId.Should().Be(first.CheckoutId);
        second.CheckoutUrl.Should().Be(first.CheckoutUrl);
        _sessions.Items.Should().HaveCount(1);
        await _adapter.Received(1).GenerateCheckoutAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>());
    }

    [Test]
    public async Task Create_DifferentPayloadSameKey_IdempotencyConflict()
    {
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs_test_z", "cs_test_z");

        await _create.Handle(ValidCommand(idempotencyKey: "key-2", amount: 50m), CancellationToken.None);

        var act = async () => await _create.Handle(
            ValidCommand(idempotencyKey: "key-2", amount: 99m), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.IdempotencyConflict);
    }

    [Test]
    public async Task Create_NoActiveConfig_PaymentsNotConfigured()
    {
        _configRepo.GetAllByTenantIdAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration>());

        var act = async () => await _create.Handle(ValidCommand(), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.PaymentsNotConfigured);
        await _adapter.DidNotReceive().GenerateCheckoutAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<Dictionary<string, string>>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>());
        _sessions.Items.Should().BeEmpty();
    }

    [Test]
    public async Task Create_ExplicitStripe_WhenOnlyBillplzActive_NotConfigured()
    {
        var billplz = new TenantPaymentConfiguration(OrgId, "BILLPLZ", "key", null, "coll", true);
        _configRepo.GetAllByTenantIdAsync(OrgId, Arg.Any<CancellationToken>())
            .Returns(new List<TenantPaymentConfiguration> { billplz });
        _configRepo.GetByTenantAndGatewayAsync(OrgId, "BILLPLZ", Arg.Any<CancellationToken>())
            .Returns(billplz);
        _configRepo.GetByTenantAndGatewayAsync(OrgId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns((TenantPaymentConfiguration?)null);

        var act = async () => await _create.Handle(ValidCommand(gatewayName: "STRIPE"), CancellationToken.None);

        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.PaymentsNotConfigured);
    }

    [Test]
    public async Task Create_ExplicitStripe_WhenStripeActive_UsesStripe()
    {
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs_test", "cs_test");

        var result = await _create.Handle(ValidCommand(gatewayName: "STRIPE"), CancellationToken.None);
        result.Gateway.Should().Be("STRIPE");
    }

    [Test]
    public async Task Create_OmitGateway_OnlyStripeActive_ResolvesStripe_NotBillplz()
    {
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs_auto", "cs_auto");

        var result = await _create.Handle(ValidCommand(gatewayName: null), CancellationToken.None);
        result.Gateway.Should().Be("STRIPE");
    }

    [Test]
    public async Task Create_BillplzMock_ReturnsBillplzGateway()
    {
        WithActiveGateway("BILLPLZ");
        AdapterReturns("https://www.billplz.com/bills/bill_1", "bill_1");

        var result = await _create.Handle(ValidCommand(gatewayName: "BILLPLZ"), CancellationToken.None);
        result.Gateway.Should().Be("BILLPLZ");
        result.ProviderSessionId.Should().Be("bill_1");
    }

    [Test]
    public async Task Create_MissingSuccessUrl_UrlsRequired()
    {
        WithActiveGateway("STRIPE");
        var cmd = ValidCommand() with { SuccessUrl = "" };

        var act = async () => await _create.Handle(cmd, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.UrlsRequired);
    }

    [Test]
    public async Task Create_AmountZero_AmountInvalid()
    {
        WithActiveGateway("STRIPE");
        var act = async () => await _create.Handle(ValidCommand(amount: 0m), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.AmountInvalid);
    }

    [Test]
    public async Task Create_AmountBelowMin_AmountBelowMinimum()
    {
        WithActiveGateway("STRIPE");
        var act = async () => await _create.Handle(ValidCommand(amount: 1.50m), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.AmountBelowMinimum);
    }

    [Test]
    public async Task Create_MetadataTooManyKeys_MetadataInvalid()
    {
        WithActiveGateway("STRIPE");
        var meta = Enumerable.Range(0, 21).ToDictionary(i => $"k{i}", i => $"v{i}");
        var act = async () => await _create.Handle(ValidCommand(metadata: meta), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.MetadataInvalid);
    }

    [Test]
    public async Task Create_AdapterFailure_GatewayError_MarksFailed()
    {
        WithActiveGateway("STRIPE");
        _adapter.GenerateCheckoutAsync(
                Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<Dictionary<string, string>>(), Arg.Any<string?>(), Arg.Any<bool>(), Arg.Any<int>())
            .Returns(new GatewayCheckoutResult(false, null, null, "stripe down"));

        var act = async () => await _create.Handle(ValidCommand(), CancellationToken.None);
        var ex = await act.Should().ThrowAsync<PaymentIntegrationException>();
        ex.Which.Code.Should().Be(PaymentErrorCodes.GatewayError);
        _sessions.Items.Should().HaveCount(1);
        _sessions.Items[0].Status.Should().Be(IntegrationCheckoutSession.StatusFailed);
    }

    [Test]
    public async Task Get_OwnCheckout_ReturnsMetadataAndAmount()
    {
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs", "cs");
        var created = await _create.Handle(ValidCommand(), CancellationToken.None);

        var got = await _get.Handle(new GetIntegrationCheckoutQuery(OrgId, created.CheckoutId), CancellationToken.None);

        got.Should().NotBeNull();
        got!.Amount.Should().Be(50m);
        got.Currency.Should().Be("MYR");
        got.Metadata["booking_id"].Should().Be("b-1");
        got.ProviderSessionId.Should().Be("cs");
    }

    [Test]
    public async Task Get_OtherOrg_ReturnsNull()
    {
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/cs", "cs");
        var created = await _create.Handle(ValidCommand(), CancellationToken.None);

        var got = await _get.Handle(new GetIntegrationCheckoutQuery(OtherOrgId, created.CheckoutId), CancellationToken.None);
        got.Should().BeNull();
    }

    [Test]
    public async Task Get_OpenPastTtl_ReturnsExpired()
    {
        var session = new IntegrationCheckoutSession(
            OrgId, 50m, "MYR", "Stale", "a@b.com",
            "https://ok", "https://cancel", "STRIPE", "{}",
            setupFutureUsage: false,
            expiresAt: DateTime.UtcNow.AddHours(-25));
        session.MarkProviderIssued("https://pay.example/cs", "cs_stale");
        _sessions.Add(session);

        var got = await _get.Handle(new GetIntegrationCheckoutQuery(OrgId, session.Id), CancellationToken.None);

        got.Should().NotBeNull();
        got!.Status.Should().Be(IntegrationCheckoutSession.StatusExpired);
        session.Status.Should().Be(IntegrationCheckoutSession.StatusExpired);
    }

    [Test]
    public async Task StringQueryWrapper_StillReturnsUrlOnly_WithLegacyFallbackPath()
    {
        // Commerce path: requireActiveGateway=false, BILLPLZ fallback when no config then throws free-text.
        WithActiveGateway("STRIPE");
        AdapterReturns("https://checkout.stripe.com/c/pay/legacy", "cs_legacy");

        var handler = new GenerateCheckoutSessionQueryHandler(_cashier);
        var url = await handler.Handle(new GenerateCheckoutSessionQuery(
            OrgId, 10m, "MYR", "Product", "a@b.com",
            "https://ok", "https://cancel",
            new Dictionary<string, string> { ["type"] = "commerce_subscription" }), CancellationToken.None);

        url.Should().Be("https://checkout.stripe.com/c/pay/legacy");
    }

    private sealed class InMemorySessionRepo : IIntegrationCheckoutSessionRepository
    {
        public List<IntegrationCheckoutSession> Items { get; } = new();

        public Task<IntegrationCheckoutSession?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
        {
            var s = Items.FirstOrDefault(x => x.Id == id && x.OrganizationId == organizationId);
            return Task.FromResult(s);
        }

        public Task<IntegrationCheckoutSession?> GetByIdempotencyKeyAsync(
            Guid organizationId, string idempotencyKey, CancellationToken ct = default)
        {
            var s = Items.FirstOrDefault(x =>
                x.OrganizationId == organizationId && x.IdempotencyKey == idempotencyKey);
            return Task.FromResult(s);
        }

        public Task<IntegrationCheckoutSession?> GetByProviderSessionIdAsync(
            Guid organizationId, string providerSessionId, CancellationToken ct = default)
        {
            var s = Items.FirstOrDefault(x =>
                x.OrganizationId == organizationId && x.ProviderSessionId == providerSessionId);
            return Task.FromResult(s);
        }

        public void Add(IntegrationCheckoutSession session) => Items.Add(session);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
