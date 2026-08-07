using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Commerce.Contracts.Events;
using Modules.Payments.Application.Ports;
using Modules.Payments.Application.Services;
using Modules.Payments.Contracts.Events;
using Modules.Payments.Domain.Aggregates;
using Modules.Payments.Infrastructure.EventHandlers;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Payments;

/// <summary>
/// Phase 3: M2M IntegrationCheckoutSession → workspace outbound payment.* webhooks.
/// </summary>
[TestFixture]
public class IntegrationCheckoutOutboundWebhookTests
{
    private static readonly Guid OrgId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid OtherOrgId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private InMemorySessionRepo _sessions = null!;
    private IEventBus _eventBus = null!;
    private IntegrationCheckoutGatewayEventsHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _sessions = new InMemorySessionRepo();
        _eventBus = Substitute.For<IEventBus>();
        _handler = new IntegrationCheckoutGatewayEventsHandler(
            _sessions,
            _eventBus,
            NullLogger<IntegrationCheckoutGatewayEventsHandler>.Instance);
    }

    private IntegrationCheckoutSession AddOpenSession(
        Guid? id = null,
        Guid? orgId = null,
        decimal amount = 50m,
        string currency = "MYR",
        Dictionary<string, string>? clientMetadata = null,
        string gatewayName = "STRIPE",
        string? providerSessionId = "cs_test_123",
        string? checkoutUrl = null)
    {
        var sessionId = id ?? Guid.CreateVersion7();
        var organizationId = orgId ?? OrgId;
        var meta = clientMetadata ?? new Dictionary<string, string>
        {
            ["integrator"] = "aura",
            ["type"] = "booking_payment",
            ["booking_id"] = "b-42",
            ["payment_type"] = "deposit"
        };
        var stamped = IntegrationCheckoutMetadata.Stamp(meta, organizationId, sessionId, "Aina");
        var session = new IntegrationCheckoutSession(
            organizationId,
            amount,
            currency,
            "Booking deposit #42",
            "guest@example.com",
            "https://app.aura.example/ok",
            "https://app.aura.example/cancel",
            gatewayName,
            IntegrationCheckoutMetadata.Serialize(stamped),
            setupFutureUsage: false,
            customerName: "Aina",
            id: sessionId);
        session.MarkProviderIssued(
            checkoutUrl ?? $"https://checkout.example/{providerSessionId ?? "none"}",
            providerSessionId);
        _sessions.Add(session);
        return session;
    }

    private static GatewayPaymentCompletedIntegrationEvent CompletedEvent(
        Guid orgId,
        Guid checkoutId,
        decimal amountPaid = 50m,
        string currency = "MYR",
        string gatewayTxId = "pi_test_completed",
        Dictionary<string, string>? extraMetadata = null)
    {
        var metadata = new Dictionary<string, string>
        {
            ["checkout_id"] = checkoutId.ToString(),
            ["tenant_id"] = orgId.ToString(),
            ["hub_workspace_id"] = orgId.ToString(),
            ["integrator"] = "aura",
            ["type"] = "booking_payment",
            ["booking_id"] = "b-42"
        };
        if (extraMetadata != null)
        {
            foreach (var (k, v) in extraMetadata)
                metadata[k] = v;
        }

        return new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: orgId,
            GatewayTransactionId: gatewayTxId,
            AmountPaid: amountPaid,
            Currency: currency,
            GatewayFee: 1.2m,
            TaxAmount: 0,
            NetAmount: amountPaid - 1.2m,
            FxRate: 1,
            BaseCurrency: currency,
            LineItems: new List<LineItemDto>(),
            Metadata: metadata);
    }

    private static GatewayPaymentFailedIntegrationEvent FailedEvent(
        Guid orgId,
        Guid checkoutId,
        string gatewayTxId = "pi_test_failed")
    {
        return new GatewayPaymentFailedIntegrationEvent(
            OrganizationId: orgId,
            GatewayTransactionId: gatewayTxId,
            Metadata: new Dictionary<string, string>
            {
                ["checkout_id"] = checkoutId.ToString(),
                ["tenant_id"] = orgId.ToString()
            });
    }

    [Test]
    public async Task Completed_OpenSession_MarksCompleted_AndPublishesPaymentCompleted_Once()
    {
        var session = AddOpenSession();
        var @event = CompletedEvent(OrgId, session.Id, amountPaid: 50m, currency: "MYR", gatewayTxId: "pi_abc");

        await _handler.HandleAsync(@event);

        session.Status.Should().Be(IntegrationCheckoutSession.StatusCompleted);
        session.GatewayTransactionId.Should().Be("pi_abc");

        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.OrganizationId == OrgId
            && e.EventType == IntegrationCheckoutGatewayEventsHandler.EventTypeCompleted
            && e.TargetUrl == null));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == IntegrationCheckoutGatewayEventsHandler.EventTypeFailed));

        _sessions.SaveCount.Should().Be(1);
    }

    [Test]
    public async Task Completed_Payload_UsesVerifiedAmount_AndSessionMetadata()
    {
        var session = AddOpenSession(amount: 99m, currency: "MYR");
        // Verified paid amount differs from session claim — outbound must use event amount.
        var @event = CompletedEvent(OrgId, session.Id, amountPaid: 50.25m, currency: "MYR", gatewayTxId: "pi_verified");

        OutboundWebhookRequestedIntegrationEvent? published = null;
        _eventBus.When(x => x.PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>()))
            .Do(ci => published = ci.Arg<OutboundWebhookRequestedIntegrationEvent>());

        await _handler.HandleAsync(@event);

        published.Should().NotBeNull();
        published!.EventType.Should().Be("payment.completed");
        published.TargetUrl.Should().BeNull();

        var data = published.Payload;
        data.GetProperty("checkout_id").GetString().Should().Be(session.Id.ToString());
        data.GetProperty("gateway").GetString().Should().Be("STRIPE");
        data.GetProperty("gateway_transaction_id").GetString().Should().Be("pi_verified");
        data.GetProperty("provider_session_id").GetString().Should().Be("cs_test_123");
        data.GetProperty("amount").GetDecimal().Should().Be(50.25m);
        data.GetProperty("currency").GetString().Should().Be("MYR");
        data.GetProperty("status").GetString().Should().Be("completed");
        data.GetProperty("event_id").GetString().Should().Be(@event.Id.ToString());
        data.GetProperty("description").GetString().Should().Be("Booking deposit #42");
        data.GetProperty("customer_email").GetString().Should().Be("guest@example.com");

        var meta = data.GetProperty("metadata");
        meta.GetProperty("integrator").GetString().Should().Be("aura");
        meta.GetProperty("booking_id").GetString().Should().Be("b-42");
        meta.GetProperty("type").GetString().Should().Be("booking_payment");
        meta.GetProperty("checkout_id").GetString().Should().Be(session.Id.ToString());
        meta.GetProperty("hub_checkout_kind").GetString().Should().Be("integration");
    }

    [Test]
    public async Task Completed_NoSession_NoPublish()
    {
        var orphanCheckoutId = Guid.CreateVersion7();
        await _handler.HandleAsync(CompletedEvent(OrgId, orphanCheckoutId));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
        _sessions.SaveCount.Should().Be(0);
    }

    [Test]
    public async Task Completed_OtherOrgSession_NoPublish()
    {
        var session = AddOpenSession(orgId: OtherOrgId);
        // Event claims OrgId but session lives under OtherOrgId — GetById is org-scoped.
        await _handler.HandleAsync(CompletedEvent(OrgId, session.Id));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
        session.Status.Should().Be(IntegrationCheckoutSession.StatusOpen);
    }

    [Test]
    public async Task Completed_AlreadyCompleted_NoSecondPublish()
    {
        var session = AddOpenSession();
        var first = CompletedEvent(OrgId, session.Id, gatewayTxId: "pi_first");
        await _handler.HandleAsync(first);

        _eventBus.ClearReceivedCalls();
        _sessions.SaveCount = 0;

        var second = CompletedEvent(OrgId, session.Id, gatewayTxId: "pi_second");
        await _handler.HandleAsync(second);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
        _sessions.SaveCount.Should().Be(0);
        session.GatewayTransactionId.Should().Be("pi_first");
    }

    [Test]
    public async Task Completed_MissingCheckoutId_NoPublish()
    {
        AddOpenSession();
        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: OrgId,
            GatewayTransactionId: "pi_no_meta",
            AmountPaid: 10m,
            Currency: "MYR",
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: 10m,
            FxRate: 1,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string> { ["type"] = "commerce_subscription" });

        await _handler.HandleAsync(@event);

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    }

    [Test]
    public async Task Failed_OpenSession_MarksFailed_AndPublishesPaymentFailed()
    {
        var session = AddOpenSession(amount: 75m, currency: "SGD");
        var @event = FailedEvent(OrgId, session.Id, gatewayTxId: "pi_fail_1");

        OutboundWebhookRequestedIntegrationEvent? published = null;
        _eventBus.When(x => x.PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>()))
            .Do(ci => published = ci.Arg<OutboundWebhookRequestedIntegrationEvent>());

        await _handler.HandleAsync(@event);

        session.Status.Should().Be(IntegrationCheckoutSession.StatusFailed);

        published.Should().NotBeNull();
        published!.EventType.Should().Be(IntegrationCheckoutGatewayEventsHandler.EventTypeFailed);
        published.TargetUrl.Should().BeNull();
        published.OrganizationId.Should().Be(OrgId);

        var data = published.Payload;
        data.GetProperty("status").GetString().Should().Be("failed");
        data.GetProperty("checkout_id").GetString().Should().Be(session.Id.ToString());
        data.GetProperty("gateway_transaction_id").GetString().Should().Be("pi_fail_1");
        // Amount/currency from session when failed event lacks money fields.
        data.GetProperty("amount").GetDecimal().Should().Be(75m);
        data.GetProperty("currency").GetString().Should().Be("SGD");
        data.GetProperty("metadata").GetProperty("booking_id").GetString().Should().Be("b-42");
    }

    [Test]
    public async Task Failed_AlreadyFailed_NoSecondPublish()
    {
        var session = AddOpenSession();
        await _handler.HandleAsync(FailedEvent(OrgId, session.Id, "pi_f1"));

        _eventBus.ClearReceivedCalls();
        await _handler.HandleAsync(FailedEvent(OrgId, session.Id, "pi_f2"));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    }

    [Test]
    public async Task Failed_MissingCheckoutId_NoPublish()
    {
        AddOpenSession();
        await _handler.HandleAsync(new GatewayPaymentFailedIntegrationEvent(
            OrgId,
            "pi_orphan_fail",
            new Dictionary<string, string> { ["type"] = "commerce_subscription" }));

        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    }

    [Test]
    public async Task Completed_StrippedMetadata_ResolvesByProviderSessionId_AndPublishesOnce()
    {
        // Billplz-like: callback body has bill id only; no checkout_id in event metadata.
        const string billId = "bill_stripped_xyz";
        var session = AddOpenSession(
            gatewayName: "BILLPLZ",
            providerSessionId: billId,
            checkoutUrl: $"https://www.billplz.com/bills/{billId}");

        var @event = new GatewayPaymentCompletedIntegrationEvent(
            OrganizationId: OrgId,
            GatewayTransactionId: billId,
            AmountPaid: 50m,
            Currency: "MYR",
            GatewayFee: 0,
            TaxAmount: 0,
            NetAmount: 50m,
            FxRate: 1,
            BaseCurrency: "MYR",
            LineItems: new List<LineItemDto>(),
            Metadata: new Dictionary<string, string> { ["type"] = "booking_payment" });

        OutboundWebhookRequestedIntegrationEvent? published = null;
        _eventBus.When(x => x.PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>()))
            .Do(ci => published = ci.Arg<OutboundWebhookRequestedIntegrationEvent>());

        await _handler.HandleAsync(@event);

        session.Status.Should().Be(IntegrationCheckoutSession.StatusCompleted);
        session.GatewayTransactionId.Should().Be(billId);

        published.Should().NotBeNull();
        published!.EventType.Should().Be(IntegrationCheckoutGatewayEventsHandler.EventTypeCompleted);
        published.Payload.GetProperty("checkout_id").GetString().Should().Be(session.Id.ToString());
        published.Payload.GetProperty("gateway_transaction_id").GetString().Should().Be(billId);
        published.Payload.GetProperty("metadata").GetProperty("booking_id").GetString().Should().Be("b-42");
        published.Payload.GetProperty("metadata").GetProperty("checkout_id").GetString().Should().Be(session.Id.ToString());

        // Dual-event / replay: same bill id after complete → no second outbound.
        _eventBus.ClearReceivedCalls();
        await _handler.HandleAsync(@event);
        await _eventBus.DidNotReceive().PublishAsync(Arg.Any<OutboundWebhookRequestedIntegrationEvent>());
    }

    [Test]
    public async Task Completed_DualEvents_SameSession_SingleOutbound()
    {
        var session = AddOpenSession();
        var first = CompletedEvent(OrgId, session.Id, gatewayTxId: "pi_shared");
        var second = CompletedEvent(OrgId, session.Id, gatewayTxId: "pi_shared");

        await _handler.HandleAsync(first);
        await _handler.HandleAsync(second);

        await _eventBus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == IntegrationCheckoutGatewayEventsHandler.EventTypeCompleted));
        session.Status.Should().Be(IntegrationCheckoutSession.StatusCompleted);
    }

    private sealed class InMemorySessionRepo : IIntegrationCheckoutSessionRepository
    {
        public List<IntegrationCheckoutSession> Items { get; } = new();
        public int SaveCount { get; set; }

        public Task<IntegrationCheckoutSession?> GetByIdAsync(Guid organizationId, Guid id, CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s => s.Id == id && s.OrganizationId == organizationId));

        public Task<IntegrationCheckoutSession?> GetByIdempotencyKeyAsync(
            Guid organizationId,
            string idempotencyKey,
            CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s =>
                s.OrganizationId == organizationId && s.IdempotencyKey == idempotencyKey));

        public Task<IntegrationCheckoutSession?> GetByProviderSessionIdAsync(
            Guid organizationId,
            string providerSessionId,
            CancellationToken ct = default)
            => Task.FromResult(Items.FirstOrDefault(s =>
                s.OrganizationId == organizationId && s.ProviderSessionId == providerSessionId));

        public void Add(IntegrationCheckoutSession session) => Items.Add(session);

        public Task SaveChangesAsync(CancellationToken ct = default)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }
}
