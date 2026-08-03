using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Modules.Commerce.Application.EventHandlers;
using Modules.Commerce.Contracts.Events;
using NSubstitute;
using NUnit.Framework;

namespace Lazuar.ModuleTests.Commerce;

/// <summary>
/// B.9 / acceptance: subscription lifecycle publishes outbound webhook requests
/// with null TargetUrl so One fans out without product URL equality.
/// </summary>
[TestFixture]
public class SubscriptionLifecycleWebhookTests
{
    [Test]
    public async Task SubscriptionActivated_Publishes_OutboundWebhook_With_Null_TargetUrl()
    {
        var bus = Substitute.For<IEventBus>();
        var handler = new SubscriptionLifecycleIntegrationEventHandlers(bus);

        var orgId = Guid.CreateVersion7();
        var subscriptionId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        await handler.HandleAsync(new SubscriptionActivatedIntegrationEvent(
            orgId,
            subscriptionId,
            clientId,
            productId,
            FulfillmentTargets: new List<string> { "https://product-form.example/never-used-for-gate" },
            IsFirstPayment: true));

        await bus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.OrganizationId == orgId
            && e.EventType == "subscription.activated"
            && e.TargetUrl == null));
    }

    [Test]
    public async Task SubscriptionSuspended_Canceled_Resumed_Publish_Matching_Event_Types()
    {
        var bus = Substitute.For<IEventBus>();
        var handler = new SubscriptionLifecycleIntegrationEventHandlers(bus);

        var orgId = Guid.CreateVersion7();
        var subId = Guid.CreateVersion7();
        var clientId = Guid.CreateVersion7();
        var productId = Guid.CreateVersion7();

        var emptyTargets = new List<string>();
        await handler.HandleAsync(new SubscriptionSuspendedIntegrationEvent(orgId, subId, clientId, productId, emptyTargets));
        await handler.HandleAsync(new SubscriptionCanceledIntegrationEvent(orgId, subId, clientId, productId, emptyTargets));
        await handler.HandleAsync(new SubscriptionResumedIntegrationEvent(orgId, subId, clientId, productId, emptyTargets));

        await bus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.suspended" && e.TargetUrl == null));
        await bus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.canceled" && e.TargetUrl == null));
        await bus.Received(1).PublishAsync(Arg.Is<OutboundWebhookRequestedIntegrationEvent>(e =>
            e.EventType == "subscription.resumed" && e.TargetUrl == null));
    }
}
