using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using FluentAssertions;
using Lazuar.ApiTypes;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Modules.Commerce.Application.EventHandlers;
using Modules.Commerce.Contracts.Events;
using Modules.Commerce.Infrastructure;
using Modules.Commerce.Infrastructure.Repositories;
using Modules.CRM.Contracts;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// LP-132: second-hop Commerce handlers must flush OutboundWebhookRequested onto
/// commerce.OutboxMessages. A mock IEventBus cannot prove this.
/// </summary>
[TestFixture]
public class OutboundWebhookRequestedPersistTests
{
    [Test]
    public async Task SubscriptionActivated_HandleAsync_Writes_OutboundWebhookRequested_To_Commerce_Outbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var handler = CreateLifecycleHandler(db);

        await handler.HandleAsync(new SubscriptionActivatedIntegrationEvent(
            orgId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            FulfillmentTargets: new List<string>(),
            IsFirstPayment: true));

        await AssertOutboundOutboxAsync(db, orgId, "subscription.activated");
    }

    [TestCase("subscription.resumed")]
    [TestCase("subscription.suspended")]
    [TestCase("subscription.canceled")]
    public async Task SubscriptionLifecycle_HandleAsync_Writes_OutboundWebhookRequested_To_Commerce_Outbox(string eventType)
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();
        var handler = CreateLifecycleHandler(db);

        await HandleLifecycleAsync(handler, eventType, orgId, subscriptionId, clientId, productId);

        await AssertOutboundOutboxAsync(db, orgId, eventType);
    }

    [Test]
    public async Task OrderCompleted_HandleAsync_Writes_OutboundWebhookRequested_To_Commerce_Outbox()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        var handler = new OrderCompletedIntegrationEventHandler(
            new OutboxEventBus<CommerceDbContext>(db),
            new CommerceRepository(db));

        await handler.HandleAsync(new OrderCompletedIntegrationEvent(
            orgId,
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            Guid.CreateVersion7(),
            new List<string>()));

        await AssertOutboundOutboxAsync(db, orgId, "order.completed");
    }

    private static CommerceDbContext CreateDb()
        => new(
            InMemoryDb.CreateOptions<CommerceDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

    private static SubscriptionLifecycleIntegrationEventHandlers CreateLifecycleHandler(CommerceDbContext db)
    {
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>()).Returns((ClientProfileDto?)null);
        return new SubscriptionLifecycleIntegrationEventHandlers(
            new OutboxEventBus<CommerceDbContext>(db),
            new CommerceRepository(db),
            crm);
    }

    private static Task HandleLifecycleAsync(
        SubscriptionLifecycleIntegrationEventHandlers handler,
        string eventType,
        Guid orgId,
        Guid subscriptionId,
        Guid clientId,
        Guid productId)
    {
        var emptyTargets = new List<string>();
        return eventType switch
        {
            "subscription.resumed" => handler.HandleAsync(
                new SubscriptionResumedIntegrationEvent(orgId, subscriptionId, clientId, productId, emptyTargets)),
            "subscription.suspended" => handler.HandleAsync(
                new SubscriptionSuspendedIntegrationEvent(orgId, subscriptionId, clientId, productId, emptyTargets)),
            "subscription.canceled" => handler.HandleAsync(
                new SubscriptionCanceledIntegrationEvent(orgId, subscriptionId, clientId, productId, emptyTargets)),
            _ => throw new ArgumentOutOfRangeException(nameof(eventType), eventType, null)
        };
    }

    private static async Task AssertOutboundOutboxAsync(CommerceDbContext db, Guid orgId, string eventType)
    {
        var row = await db.OutboxMessages.SingleAsync();
        row.Type.Should().Contain(nameof(OutboundWebhookRequestedIntegrationEvent));
        row.ProcessedAt.Should().BeNull();

        using var doc = JsonDocument.Parse(row.Data);
        doc.RootElement.GetProperty("EventType").GetString().Should().Be(eventType);
        doc.RootElement.GetProperty("OrganizationId").GetGuid().Should().Be(orgId);

        if (doc.RootElement.TryGetProperty("TargetUrl", out var targetUrl))
        {
            targetUrl.ValueKind.Should().Be(JsonValueKind.Null);
        }
    }
}
