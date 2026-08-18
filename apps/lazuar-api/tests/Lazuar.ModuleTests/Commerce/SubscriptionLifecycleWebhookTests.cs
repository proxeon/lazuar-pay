using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using Lazuar.ApiTypes;
using Modules.Commerce.Application;
using Modules.Commerce.Application.EventHandlers;
using Modules.Commerce.Contracts.Events;
using Modules.CRM.Contracts;
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
        var (handler, bus, repo) = CreateHandler();

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
        await repo.Received(1).SaveChangesAsync();
    }

    [Test]
    public async Task SubscriptionSuspended_Canceled_Resumed_Publish_Matching_Event_Types()
    {
        var (handler, bus, repo) = CreateHandler();

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
        await repo.Received(3).SaveChangesAsync();
    }

    [Test]
    public void Payload_Includes_PaidThrough_And_AuraOrgId()
    {
        var orgId = Guid.CreateVersion7();
        var auraOrgId = Guid.CreateVersion7();
        var product = new Modules.Commerce.Domain.Aggregates.Product(
            orgId, "Aura Pro monthly", "aura-pro-monthly", 149m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new Modules.Commerce.Domain.ValueObjects.CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
        var sub = new Modules.Commerce.Domain.Aggregates.Subscription(orgId, Guid.CreateVersion7(), product.Id);
        var next = new DateTime(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);
        sub.Activate(DateTime.UtcNow, next);
        sub.SetMetadataJson(CommerceCheckoutMetadata.Serialize(new Dictionary<string, string>
        {
            ["aura_org_id"] = auraOrgId.ToString(),
            ["type"] = "saas_subscription",
            ["billing_interval"] = "monthly"
        }));

        var payload = CommerceWebhookPayload.From(sub, product, "owner@salon.example", "ACTIVE", isFirstPayment: true);

        Assert.That(payload.GetProperty("subscription_id").GetString(), Is.EqualTo(sub.Id.ToString()));
        Assert.That(payload.GetProperty("customer_id").GetString(), Is.EqualTo(sub.ClientProfileId.ToString()));
        Assert.That(payload.GetProperty("client_profile_id").GetString(), Is.EqualTo(sub.ClientProfileId.ToString()));
        Assert.That(payload.GetProperty("status").GetString(), Is.EqualTo("ACTIVE"));
        Assert.That(payload.GetProperty("current_period_end").GetDateTime(), Is.EqualTo(next));
        Assert.That(payload.GetProperty("customer_email").GetString(), Is.EqualTo("owner@salon.example"));
        Assert.That(payload.GetProperty("amount").GetDecimal(), Is.EqualTo(149m));
        Assert.That(payload.GetProperty("currency").GetString(), Is.EqualTo("MYR"));
        Assert.That(payload.GetProperty("interval").GetString(), Is.EqualTo("mo"));
        Assert.That(payload.GetProperty("is_first_payment").GetBoolean(), Is.True);
        Assert.That(payload.GetProperty("metadata").GetProperty("aura_org_id").GetString(), Is.EqualTo(auraOrgId.ToString()));
        Assert.That(payload.GetProperty("metadata").GetProperty("type").GetString(), Is.EqualTo("saas_subscription"));
    }

    [TestCase("subscription.activated", "ACTIVE")]
    [TestCase("subscription.resumed", "ACTIVE")]
    [TestCase("subscription.past_due", "PAST_DUE")]
    [TestCase("subscription.canceled", "CANCELED")]
    [TestCase("subscription.suspended", "SUSPENDED")]
    public void Payload_FiveEventTypes_ShareRequiredFields(string _, string status)
    {
        var orgId = Guid.CreateVersion7();
        var product = new Modules.Commerce.Domain.Aggregates.Product(
            orgId, "Plan", "plan", 1490m, "FIXED", 0m, "MYR", "yr", "STRIPE",
            new Modules.Commerce.Domain.ValueObjects.CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
        var sub = new Modules.Commerce.Domain.Aggregates.Subscription(orgId, Guid.CreateVersion7(), product.Id);
        var next = DateTime.UtcNow.AddYears(1);
        sub.Activate(DateTime.UtcNow, next);
        var auraOrgId = Guid.CreateVersion7();
        sub.SetMetadataJson(JsonSerializer.Serialize(new Dictionary<string, string>
        {
            ["aura_org_id"] = auraOrgId.ToString(),
            ["type"] = "saas_subscription"
        }));

        var payload = CommerceWebhookPayload.From(sub, product, "a@b.c", status);

        Assert.That(payload.GetProperty("subscription_id").GetString(), Is.EqualTo(sub.Id.ToString()));
        Assert.That(payload.GetProperty("status").GetString(), Is.EqualTo(status));
        Assert.That(payload.GetProperty("current_period_end").ValueKind, Is.EqualTo(JsonValueKind.String));
        Assert.That(payload.GetProperty("metadata").GetProperty("aura_org_id").GetString(), Is.EqualTo(auraOrgId.ToString()));
    }

    [Test]
    public void Payload_ActivateTrial_EmitsTrialingAndZeroAmount()
    {
        var orgId = Guid.CreateVersion7();
        var product = new Modules.Commerce.Domain.Aggregates.Product(
            orgId, "Plan", "plan", 149m, "FIXED", 0m, "MYR", "mo", "STRIPE",
            new Modules.Commerce.Domain.ValueObjects.CheckoutConfiguration(false, false, false),
            Array.Empty<string>());
        var sub = new Modules.Commerce.Domain.Aggregates.Subscription(orgId, Guid.CreateVersion7(), product.Id);
        sub.ActivateTrial(DateTime.UtcNow.AddDays(14), reminderOnly: false);

        var payload = CommerceWebhookPayload.From(sub, product, "a@b.c", "TRIALING");

        Assert.That(payload.GetProperty("status").GetString(), Is.EqualTo("TRIALING"));
        Assert.That(payload.GetProperty("amount").GetDecimal(), Is.EqualTo(0m));
    }

    private static (SubscriptionLifecycleIntegrationEventHandlers Handler, IEventBus Bus, ICommerceRepository Repo) CreateHandler()
    {
        var bus = Substitute.For<IEventBus>();
        var repo = Substitute.For<ICommerceRepository>();
        var crm = Substitute.For<ICrmQueryService>();
        crm.GetClientProfileAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns((ClientProfileDto?)null);
        return (new SubscriptionLifecycleIntegrationEventHandlers(bus, repo, crm), bus, repo);
    }
}
