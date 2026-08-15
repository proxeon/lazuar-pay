using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Payments.Application.Commands;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Services;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Domain.Entities;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

[TestFixture]
public class ProcessGatewayWebhookCommandHandlerTests
{
    private static ISecretVault CreateVault() =>
        new AesSecretVault(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Kms:MasterKey"] = "test-master-key-for-unit-tests-32"
            })
            .Build());

    private static ProcessGatewayWebhookCommandHandler CreateHandler(
        ITenantPaymentConfigRepository configRepo,
        IPaymentWebhookLogRepository logRepo,
        IPaymentGatewayFactory gatewayFactory,
        IEventBus eventBus,
        IIntegrationCheckoutSessionRepository? sessions = null)
        => new(
            configRepo,
            logRepo,
            gatewayFactory,
            eventBus,
            CreateVault(),
            sessions ?? new EmptySessionRepo(),
            NullLogger<ProcessGatewayWebhookCommandHandler>.Instance);

    [Test]
    public async Task Handle_PaymentFailed_Publishes_GatewayPaymentFailedIntegrationEvent()
    {
        var tenantId = Guid.CreateVersion7();
        var metadata = new Dictionary<string, string>
        {
            ["type"] = "commerce_subscription",
            ["subscription_id"] = Guid.CreateVersion7().ToString()
        };

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        logRepo.HasBusinessKeyBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.GatewayType.Returns("STRIPE");
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: "PAYMENT_FAILED",
                EventId: "evt_failed_1",
                AmountPaid: 0,
                Currency: "MYR",
                GatewayTransactionId: "pi_failed_1",
                Metadata: metadata,
                GatewayFee: 0,
                TaxAmount: 0,
                NetAmount: 0,
                FxRate: 1,
                BaseCurrency: "MYR",
                Error: null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();

        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus);

        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.OrganizationId == tenantId
            && e.GatewayTransactionId == "pi_failed_1"
            && e.Metadata["type"] == "commerce_subscription"));

        logRepo.Received(1).Add(Arg.Is<PaymentWebhookLog>(l =>
            l.EventId == "evt_failed_1"
            && l.BusinessKey == "PAYMENT_FAILED:pi_failed_1"));
        await logRepo.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_PaymentFailed_Uses_EventId_When_GatewayTransactionId_Missing()
    {
        var tenantId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        logRepo.HasBusinessKeyBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.GatewayType.Returns("STRIPE");
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: "PAYMENT_FAILED",
                EventId: "evt_no_tx",
                AmountPaid: 0,
                Currency: "MYR",
                GatewayTransactionId: null,
                Metadata: new Dictionary<string, string>(),
                GatewayFee: 0,
                TaxAmount: 0,
                NetAmount: 0,
                FxRate: 1,
                BaseCurrency: "MYR",
                Error: null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);

        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus);

        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentFailedIntegrationEvent>(e =>
            e.GatewayTransactionId == "evt_no_tx"));

        logRepo.Received(1).Add(Arg.Is<PaymentWebhookLog>(l => l.BusinessKey == null));
        await logRepo.DidNotReceive().HasBusinessKeyBeenProcessedAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_Skips_When_EventId_Already_Processed()
    {
        var tenantId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync("evt_dup", "STRIPE", Arg.Any<CancellationToken>()).Returns(true);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                true, "PAYMENT_COMPLETED", "evt_dup", 10m, "MYR", "pi_1",
                new Dictionary<string, string>(), 0, 0, 10, 1, "MYR", null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();

        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus);
        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<GatewayPaymentCompletedIntegrationEvent>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<GatewayPaymentFailedIntegrationEvent>());
        logRepo.DidNotReceive().Add(Arg.Any<PaymentWebhookLog>());
    }

    [Test]
    public async Task Handle_Skips_When_BusinessKey_Already_Processed()
    {
        var tenantId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        logRepo.HasBusinessKeyBeenProcessedAsync("PAYMENT_COMPLETED:pi_shared", "STRIPE", Arg.Any<CancellationToken>())
            .Returns(true);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                true, "PAYMENT_COMPLETED", "evt_second", 50m, "MYR", "pi_shared",
                new Dictionary<string, string>(), 0, 0, 50, 1, "MYR", null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();

        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus);
        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.DidNotReceive().PublishAsync(Arg.Any<GatewayPaymentCompletedIntegrationEvent>());
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<GatewayPaymentFailedIntegrationEvent>());
        logRepo.DidNotReceive().Add(Arg.Any<PaymentWebhookLog>());
    }

    [Test]
    public async Task Handle_UniqueConstraintRace_Returns_WithoutRethrow()
    {
        var tenantId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        logRepo.HasBusinessKeyBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        logRepo.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.FromException(new Exception("23505: duplicate key value violates unique constraint")));

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                true, "PAYMENT_COMPLETED", "evt_race", 10m, "MYR", "pi_race",
                new Dictionary<string, string>(), 0, 0, 10, 1, "MYR", null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();

        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus);

        Assert.DoesNotThrowAsync(async () => await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None));

        await eventBus.Received(1).PublishAsync(Arg.Any<GatewayPaymentCompletedIntegrationEvent>());
    }

    [Test]
    public void IsUniqueConstraintViolation_Detects_23505_In_Message()
    {
        var ex = new InvalidOperationException("inner", new Exception("23505 unique_violation"));
        Assert.That(ProcessGatewayWebhookCommandHandler.IsUniqueConstraintViolation(ex), Is.True);
    }

    [Test]
    public void IsUniqueConstraintViolation_Returns_False_For_Other_Errors()
    {
        var ex = new InvalidOperationException("connection refused");
        Assert.That(ProcessGatewayWebhookCommandHandler.IsUniqueConstraintViolation(ex), Is.False);
    }

    [Test]
    public async Task Handle_PaymentCompleted_Merges_SessionMetadata_By_ProviderSessionId()
    {
        var tenantId = Guid.CreateVersion7();
        var checkoutId = Guid.CreateVersion7();
        const string billId = "bill_stripped_abc";

        var sessionMeta = IntegrationCheckoutMetadata.Stamp(
            new Dictionary<string, string>
            {
                ["integrator"] = "aura",
                ["type"] = "booking_payment",
                ["booking_id"] = "b-99",
                ["payment_type"] = "deposit"
            },
            tenantId,
            checkoutId,
            "Guest");

        var session = new IntegrationCheckoutSession(
            tenantId,
            50m,
            "MYR",
            "Booking deposit",
            "guest@example.com",
            "https://ok",
            "https://cancel",
            "BILLPLZ",
            IntegrationCheckoutMetadata.Serialize(sessionMeta),
            setupFutureUsage: false,
            customerName: "Guest",
            id: checkoutId);
        session.MarkProviderIssued("https://billplz.com/bills/bill_stripped_abc", billId);

        var sessions = new InMemorySessionRepo();
        sessions.Add(session);

        var config = new TenantPaymentConfiguration(tenantId, "BILLPLZ", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "BILLPLZ", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        logRepo.HasBusinessKeyBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        // Adapter returns stripped Billplz-like metadata (type only, no checkout_id).
        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.GatewayType.Returns("BILLPLZ");
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                Verified: true,
                EventType: "PAYMENT_COMPLETED",
                EventId: billId,
                AmountPaid: 50m,
                Currency: "MYR",
                GatewayTransactionId: billId,
                Metadata: new Dictionary<string, string> { ["type"] = "booking_payment" },
                GatewayFee: 0,
                TaxAmount: 0,
                NetAmount: 50m,
                FxRate: 1,
                BaseCurrency: "MYR",
                Error: null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("BILLPLZ").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();

        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus, sessions);
        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "BILLPLZ", "id=bill_stripped_abc&paid=true", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentCompletedIntegrationEvent>(e =>
            e.OrganizationId == tenantId
            && e.GatewayTransactionId == billId
            && e.Metadata["checkout_id"] == checkoutId.ToString()
            && e.Metadata["booking_id"] == "b-99"
            && e.Metadata["integrator"] == "aura"
            && e.Metadata["type"] == "booking_payment"
            && e.Metadata["hub_checkout_kind"] == "integration"
            && e.Metadata["hub_workspace_id"] == tenantId.ToString()));
    }

    [Test]
    public async Task Handle_PaymentCompleted_NoSession_Publishes_AdapterMetadataOnly()
    {
        var tenantId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "BILLPLZ", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "BILLPLZ", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);
        logRepo.HasBusinessKeyBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                true, "PAYMENT_COMPLETED", "bill_commerce", 10m, "MYR", "bill_commerce",
                new Dictionary<string, string>
                {
                    ["type"] = "commerce_subscription",
                    ["subscription_id"] = Guid.CreateVersion7().ToString()
                },
                0, 0, 10, 1, "MYR", null));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("BILLPLZ").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();

        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus, new EmptySessionRepo());
        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "BILLPLZ", "{}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentCompletedIntegrationEvent>(e =>
            e.Metadata["type"] == "commerce_subscription"
            && !e.Metadata.ContainsKey("checkout_id")));
    }

    [Test]
    public async Task Handle_StripeDualEvents_SameBusinessKey_Publishes_OnlyOnce()
    {
        var tenantId = Guid.CreateVersion7();
        var checkoutId = Guid.CreateVersion7();
        const string piId = "pi_dual_shared";
        var metadata = new Dictionary<string, string>
        {
            ["checkout_id"] = checkoutId.ToString(),
            ["hub_workspace_id"] = tenantId.ToString(),
            ["type"] = "booking_payment",
            ["booking_id"] = "b-dual"
        };

        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var processedEventIds = new HashSet<string>(StringComparer.Ordinal);
        var processedBusinessKeys = new HashSet<string>(StringComparer.Ordinal);
        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        logRepo.HasBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(processedEventIds.Contains(ci.ArgAt<string>(0))));
        logRepo.HasBusinessKeyBeenProcessedAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(processedBusinessKeys.Contains(ci.ArgAt<string>(0))));
        logRepo.When(r => r.Add(Arg.Any<PaymentWebhookLog>()))
            .Do(ci =>
            {
                var log = ci.Arg<PaymentWebhookLog>();
                processedEventIds.Add(log.EventId);
                if (!string.IsNullOrEmpty(log.BusinessKey))
                    processedBusinessKeys.Add(log.BusinessKey!);
            });

        var call = 0;
        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(_ =>
            {
                call++;
                // First: payment_intent.succeeded; second: checkout.session.completed — same PI.
                var eventId = call == 1 ? "evt_pi_succeeded" : "evt_session_completed";
                return new GatewayWebhookParsedResult(
                    true, "PAYMENT_COMPLETED", eventId, 50m, "MYR", piId,
                    new Dictionary<string, string>(metadata),
                    0, 0, 50, 1, "MYR", null);
            });

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus);

        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{\"type\":\"payment_intent.succeeded\"}", new Dictionary<string, string>()),
            CancellationToken.None);
        await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{\"type\":\"checkout.session.completed\"}", new Dictionary<string, string>()),
            CancellationToken.None);

        await eventBus.Received(1).PublishAsync(Arg.Is<GatewayPaymentCompletedIntegrationEvent>(e =>
            e.GatewayTransactionId == piId
            && e.Metadata["checkout_id"] == checkoutId.ToString()));
        await eventBus.DidNotReceive().PublishAsync(Arg.Is<GatewayPaymentCompletedIntegrationEvent>(e =>
            e.GatewayTransactionId != piId));
    }

    [Test]
    public async Task Handle_UnverifiedParse_DoesNotPublishGatewayPaymentCompleted()
    {
        var tenantId = Guid.CreateVersion7();
        var config = new TenantPaymentConfiguration(tenantId, "STRIPE", "sk_test", "whsec_test", null);
        var configRepo = Substitute.For<ITenantPaymentConfigRepository>();
        configRepo.GetByTenantAndGatewayAsync(tenantId, "STRIPE", Arg.Any<CancellationToken>())
            .Returns(config);

        var logRepo = Substitute.For<IPaymentWebhookLogRepository>();
        var adapter = Substitute.For<IPaymentGatewayAdapter>();
        adapter.ParseWebhookAsync(
                Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Dictionary<string, string>>(),
                Arg.Any<decimal>(), Arg.Any<decimal>(), Arg.Any<decimal>())
            .Returns(new GatewayWebhookParsedResult(
                Verified: false,
                EventType: "PAYMENT_COMPLETED",
                EventId: "evt_bad_sig",
                AmountPaid: 10m,
                Currency: "MYR",
                GatewayTransactionId: "pi_bad",
                Metadata: new Dictionary<string, string>
                {
                    ["type"] = "commerce_subscription",
                    ["subscription_id"] = Guid.CreateVersion7().ToString()
                },
                GatewayFee: 0,
                TaxAmount: 0,
                NetAmount: 10,
                FxRate: 1,
                BaseCurrency: "MYR",
                Error: "bad signature"));

        var gatewayFactory = Substitute.For<IPaymentGatewayFactory>();
        gatewayFactory.GetAdapter("STRIPE").Returns(adapter);
        var eventBus = Substitute.For<IEventBus>();
        var handler = CreateHandler(configRepo, logRepo, gatewayFactory, eventBus);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(async () => await handler.Handle(
            new ProcessGatewayWebhookCommand(tenantId, "STRIPE", "{}", new Dictionary<string, string>()),
            CancellationToken.None));

        Assert.That(ex!.Message, Does.Contain("verification failed"));
        await eventBus.DidNotReceive().PublishAsync(Arg.Any<GatewayPaymentCompletedIntegrationEvent>());
        logRepo.DidNotReceive().Add(Arg.Any<PaymentWebhookLog>());
    }

    private sealed class EmptySessionRepo : IIntegrationCheckoutSessionRepository
    {
        public Task<IntegrationCheckoutSession?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
            => Task.FromResult<IntegrationCheckoutSession?>(null);

        public Task<IntegrationCheckoutSession?> GetByIdempotencyKeyAsync(
            Guid organizationId, string idempotencyKey, CancellationToken ct = default)
            => Task.FromResult<IntegrationCheckoutSession?>(null);

        public Task<IntegrationCheckoutSession?> GetByProviderSessionIdAsync(
            Guid organizationId, string providerSessionId, CancellationToken ct = default)
            => Task.FromResult<IntegrationCheckoutSession?>(null);

        public void Add(IntegrationCheckoutSession session) { }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class InMemorySessionRepo : IIntegrationCheckoutSessionRepository
    {
        public List<IntegrationCheckoutSession> Items { get; } = new();

        public Task<IntegrationCheckoutSession?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.Id == id && s.OrganizationId == organizationId));

        public Task<IntegrationCheckoutSession?> GetByIdempotencyKeyAsync(
            Guid organizationId, string idempotencyKey, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s =>
                s.OrganizationId == organizationId && s.IdempotencyKey == idempotencyKey));

        public Task<IntegrationCheckoutSession?> GetByProviderSessionIdAsync(
            Guid organizationId, string providerSessionId, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s =>
                s.OrganizationId == organizationId && s.ProviderSessionId == providerSessionId));

        public void Add(IntegrationCheckoutSession session) => Items.Add(session);

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
