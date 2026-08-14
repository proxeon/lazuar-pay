using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Infrastructure;
using Lazuar.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Modules.Commerce.Contracts.Events;
using Modules.One.Domain;
using Modules.One.Infrastructure;
using Modules.One.Infrastructure.EventHandlers;
using Modules.One.Infrastructure.Workers;
using NUnit.Framework;

namespace Lazuar.ModuleTests.One;

[TestFixture]
public class OutboundWebhookTests
{
    [Test]
    public void Signature_P09_SaasEnvelope_Vector_Is_Stable()
    {
        const string secret = "whsec_p09_test";
        const long ts = 1_700_000_000L;
        const string body =
            """{"id":"evt_p09","event_type":"subscription.activated","created_at":"2023-11-14T22:13:20Z","data":{"subscription_id":"00000000-0000-0000-0000-000000000001","status":"ACTIVE"}}""";

        var header = OutboundWebhookSignature.ComputeHeaderValue(secret, body, ts);
        const string expected = "t=1700000000,v1=7fdf77fb7999da96c9aa8e06cb70a386a0f637be6e68a4a8b873f5765d3ba730";

        Assert.That(header, Is.EqualTo(expected));
        Assert.That(
            OutboundWebhookSignature.TryVerify(secret, body, expected, toleranceSeconds: 0, nowUnixSeconds: ts),
            Is.True);
    }

    [Test]
    public void Signature_Format_Is_Timestamp_And_V1_Hmac_Of_Timestamp_Dot_Body()
    {
        const string secret = "whsec_test_secret";
        const string body = """{"event_type":"order.completed"}""";
        const long ts = 1_700_000_000L;

        var header = OutboundWebhookSignature.ComputeHeaderValue(secret, body, ts);

        Assert.That(header, Does.StartWith($"t={ts},v1="));

        var expectedHex = ComputeHmacHex(secret, $"{ts}.{body}");
        Assert.That(header, Is.EqualTo($"t={ts},v1={expectedHex}"));
    }

    [Test]
    public void Signature_TryVerify_Accepts_Valid_Header_Within_Tolerance()
    {
        const string secret = "whsec_receiver_secret";
        const string body = """{"event_type":"subscription.activated","status":"ACTIVE"}""";
        const long ts = 1_720_000_000L;

        var header = OutboundWebhookSignature.ComputeHeaderValue(secret, body, ts);

        Assert.That(
            OutboundWebhookSignature.TryVerify(secret, body, header, toleranceSeconds: 300, nowUnixSeconds: ts),
            Is.True);
        Assert.That(
            OutboundWebhookSignature.TryVerify(secret, body, header, toleranceSeconds: 300, nowUnixSeconds: ts + 60),
            Is.True);
    }

    [Test]
    public void Signature_TryVerify_Rejects_Tampered_Body_Wrong_Secret_Or_Stale_Timestamp()
    {
        const string secret = "whsec_receiver_secret";
        const string body = """{"event_type":"order.completed"}""";
        const long ts = 1_720_000_000L;
        var header = OutboundWebhookSignature.ComputeHeaderValue(secret, body, ts);

        Assert.That(
            OutboundWebhookSignature.TryVerify(secret, """{"event_type":"tampered"}""", header, 300, ts),
            Is.False);
        Assert.That(
            OutboundWebhookSignature.TryVerify("whsec_other", body, header, 300, ts),
            Is.False);
        Assert.That(
            OutboundWebhookSignature.TryVerify(secret, body, header, toleranceSeconds: 30, nowUnixSeconds: ts + 120),
            Is.False);
        Assert.That(
            OutboundWebhookSignature.TryVerify(secret, body, "not-a-header", 300, ts),
            Is.False);
        Assert.That(
            OutboundWebhookSignature.TryVerify(secret, body, null, 300, ts),
            Is.False);
    }

    [Test]
    public async Task FanOut_SubscriptionActivated_Without_Product_Url_Match()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        // Workspace endpoints only — product fulfillment URL must not gate delivery.
        db.TenantWebhookEndpoints.Add(new TenantWebhookEndpoint(
            orgId,
            "https://customer.example/hooks/workspace",
            "whsec_sub_a",
            isActive: true));
        await db.SaveChangesAsync();

        var handler = new OutboundWebhookEventHandlers(db, NullLogger<OutboundWebhookEventHandlers>.Instance);
        await handler.HandleAsync(new OutboundWebhookRequestedIntegrationEvent(
            orgId,
            TargetUrl: "https://legacy-product-form.example/fulfillment",
            EventType: "subscription.activated",
            Payload: JsonSerializer.SerializeToElement(new
            {
                subscription_id = Guid.CreateVersion7().ToString(),
                status = "ACTIVE"
            })));

        var outboxes = await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().ToListAsync();
        Assert.That(outboxes, Has.Count.EqualTo(1));
        Assert.That(outboxes[0].EventType, Is.EqualTo("subscription.activated"));

        var endpoint = await db.TenantWebhookEndpoints.IgnoreQueryFilters()
            .SingleAsync(e => e.Id == outboxes[0].EndpointId);
        Assert.That(endpoint.Url, Is.EqualTo("https://customer.example/hooks/workspace"));
    }

    [Test]
    public async Task FanOut_Enqueues_All_Active_Endpoints_Without_Url_Match()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();

        // Workspace endpoint URL does NOT match any product fulfillment URL.
        db.TenantWebhookEndpoints.Add(new TenantWebhookEndpoint(
            orgId,
            "https://customer.example/hooks/workspace",
            "whsec_aaaa",
            isActive: true));
        db.TenantWebhookEndpoints.Add(new TenantWebhookEndpoint(
            orgId,
            "https://customer.example/hooks/erp",
            "whsec_bbbb",
            isActive: true,
            enabledEvents: new[] { "order.completed" }));
        // Filtered out by event type
        db.TenantWebhookEndpoints.Add(new TenantWebhookEndpoint(
            orgId,
            "https://customer.example/hooks/subs-only",
            "whsec_cccc",
            isActive: true,
            enabledEvents: new[] { "subscription.activated" }));
        // Inactive
        db.TenantWebhookEndpoints.Add(new TenantWebhookEndpoint(
            orgId,
            "https://customer.example/hooks/dead",
            "whsec_dddd",
            isActive: false));
        await db.SaveChangesAsync();

        var handler = new OutboundWebhookEventHandlers(db, NullLogger<OutboundWebhookEventHandlers>.Instance);
        var payload = JsonSerializer.SerializeToElement(new { order_id = "x" });

        // TargetUrl intentionally different — must not gate delivery.
        await handler.HandleAsync(new OutboundWebhookRequestedIntegrationEvent(
            orgId,
            TargetUrl: "https://product-form.example/never-match",
            EventType: "order.completed",
            Payload: payload));

        var outboxes = await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().ToListAsync();
        Assert.That(outboxes, Has.Count.EqualTo(2));
        Assert.That(outboxes.Select(o => o.EventType).Distinct().Single(), Is.EqualTo("order.completed"));
    }

    [Test]
    public async Task FanOut_With_Null_TargetUrl_Still_Delivers()
    {
        await using var db = CreateDb();
        var orgId = Guid.CreateVersion7();
        db.TenantWebhookEndpoints.Add(new TenantWebhookEndpoint(
            orgId, "https://hooks.example/a", "whsec_x", true));
        await db.SaveChangesAsync();

        var handler = new OutboundWebhookEventHandlers(db, NullLogger<OutboundWebhookEventHandlers>.Instance);
        await handler.HandleAsync(new OutboundWebhookRequestedIntegrationEvent(
            orgId,
            TargetUrl: null,
            EventType: "subscription.past_due",
            Payload: JsonSerializer.SerializeToElement(new { status = "PAST_DUE" })));

        Assert.That(await db.WebhookDeliveryOutboxes.IgnoreQueryFilters().CountAsync(), Is.EqualTo(1));
    }

    [Test]
    public void AcceptsEvent_Empty_Means_All()
    {
        var endpoint = new TenantWebhookEndpoint(Guid.CreateVersion7(), "https://x", "whsec_y");
        Assert.That(endpoint.AcceptsEvent("payment_link.paid"), Is.True);
        Assert.That(endpoint.AcceptsEvent("invoice.valid"), Is.True);

        endpoint.Update("https://x", true, new[] { "order.completed" });
        Assert.That(endpoint.AcceptsEvent("order.completed"), Is.True);
        Assert.That(endpoint.AcceptsEvent("subscription.activated"), Is.False);
    }

    private static OneDbContext CreateDb()
        => new(
            InMemoryDb.CreateOptions<OneDbContext>(),
            FakeExecutionContextAccessor.EmptyTenant(),
            InMemoryDb.NullMediator,
            new DatabaseJobTrigger());

    private static string ComputeHmacHex(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }
}
