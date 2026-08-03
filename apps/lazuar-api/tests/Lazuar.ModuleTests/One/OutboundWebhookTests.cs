using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using BuildingBlocks.Application;
using BuildingBlocks.Infrastructure;
using MediatR;
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

        Assert.That(await db.WebhookDeliveryOutboxes.CountAsync(), Is.EqualTo(1));
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
    {
        var options = new DbContextOptionsBuilder<OneDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new OneDbContext(options, new TestExecutionContext(), new NoopMediator(), new DatabaseJobTrigger());
    }

    private static string ComputeHmacHex(string secret, string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        using var hmac = new HMACSHA256(keyBytes);
        return Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
    }

    private sealed class TestExecutionContext : IExecutionContextAccessor
    {
        public Guid UserId => Guid.Empty;
        public Guid TenantId => Guid.Empty;
        public bool IsSystemAdmin => true;
        public string UserRole => "SUPER_ADMIN";
        public bool IsTestMode => false;
        public string AuditSignature => "test";
    }

    private sealed class NoopMediator : IMediator
    {
        public Task Publish(object notification, System.Threading.CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, System.Threading.CancellationToken cancellationToken = default)
            where TNotification : INotification
            => Task.CompletedTask;

        public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, System.Threading.CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task Send<TRequest>(TRequest request, System.Threading.CancellationToken cancellationToken = default)
            where TRequest : IRequest
            => throw new NotSupportedException();

        public Task<object?> Send(object request, System.Threading.CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<TResponse> CreateStream<TResponse>(
            IStreamRequest<TResponse> request,
            System.Threading.CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<object?> CreateStream(
            object request,
            System.Threading.CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
