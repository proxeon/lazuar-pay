using System.Net;
using System.Net.Sockets;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Webhooks.Outbound;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Regression tests for outbound webhook dispatch (issues 005 and 017 in issues/001):
/// one poison delivery must not abort or starve the batch, and the dialed address must be
/// validated at connect time (no DNS-rebinding TOCTOU between check and dial).
/// </summary>
public class OutboundDispatchTests
{
    [Test]
    public async Task Poison_endpoint_secret_errors_only_its_row_and_the_batch_continues()
    {
        // Issue 005: an undecryptable endpoint secret (key rotation, DB restored from another
        // environment) used to throw mid-loop before the single SaveChanges — nothing was
        // persisted, so every delivery behind the poison row starved forever and every row
        // before it was re-POSTed each cycle.
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK,
                    """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"},{"id":"t2","role":"owner","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);
        var client = factory.CreateClient();

        foreach (var org in new[] { "t1", "t2" })
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, $"/v1/orgs/{org}/webhooks")
            {
                Content = new StringContent("""{"url":"http://127.0.0.1:9/hook"}""", Encoding.UTF8, "application/json")
            };
            put.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
            Assert.That((await client.SendAsync(put)).IsSuccessStatusCode, Is.True);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            // Poison org t1's endpoint secret: Unprotect must throw for its deliveries.
            db.OrgWebhookEndpoints.Find("t1")!.SecretCiphertext = "!!!not-a-wrapped-secret!!!";
            var now = DateTimeOffset.UtcNow;
            db.OrgWebhookDeliveries.Add(new OrgWebhookDeliveryRow
            {
                Id = "d_poison", OrgId = "t1", EventId = "evt_poison", EventType = "payment.paid",
                PayloadJson = "{}", Status = "pending", NextAttemptAt = now,
                CreatedAt = now.AddSeconds(-10) // EARLIER: the poison row sorts first in the batch
            });
            db.OrgWebhookDeliveries.Add(new OrgWebhookDeliveryRow
            {
                Id = "d_healthy", OrgId = "t2", EventId = "evt_healthy", EventType = "payment.paid",
                PayloadJson = "{}", Status = "pending", NextAttemptAt = now,
                CreatedAt = now
            });
            await db.SaveChangesAsync();
        }

        using (var scope = factory.Services.CreateScope())
        {
            var dispatch = scope.ServiceProvider.GetRequiredService<OutboundWebhookDispatch>();
            await dispatch.ProcessBatchAsync(CancellationToken.None);
        }

        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        var poison = pay.OrgWebhookDeliveries.Single(x => x.Id == "d_poison");
        Assert.That(poison.Status, Is.EqualTo("pending"), "the poison row is held for retry, not dropped");
        Assert.That(poison.LastError, Does.StartWith("dispatch:"), "the failure is surfaced, not swallowed");
        Assert.That(poison.NextAttemptAt, Is.GreaterThan(DateTimeOffset.UtcNow), "backoff is scheduled");

        var healthy = pay.OrgWebhookDeliveries.Single(x => x.Id == "d_healthy");
        Assert.That(healthy.Status, Is.EqualTo("succeeded"),
            "a poison row ahead of it in the batch must not starve this delivery");
        Assert.That(healthy.LastHttpStatus, Is.EqualTo(200));
    }

    [Test]
    public async Task Validated_connect_refuses_private_loopback_and_metadata_addresses()
    {
        // Issue 017: the dialed address is re-validated at connect time. A rebinding DNS
        // cannot pass the dispatcher's check and then dial 127.0.0.1/10.x/169.254.169.254 —
        // the connect itself refuses.
        foreach (var host in new[] { "127.0.0.1", "169.254.169.254", "10.1.2.3", "192.168.0.10" })
        {
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await OutboundUrl.ConnectValidatedAsync(new DnsEndPoint(host, 443), allowLoopback: false, CancellationToken.None),
                $"{host} must be refused");
        }

        Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await OutboundUrl.ConnectValidatedAsync(new DnsEndPoint("localhost", 443), allowLoopback: false, CancellationToken.None),
            "localhost resolves to loopback and must be refused outside dev/test");

        // Positive control: loopback is dialable when the environment allows it.
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var acceptTask = listener.AcceptTcpClientAsync(CancellationToken.None);
        using var stream = await OutboundUrl.ConnectValidatedAsync(
            new DnsEndPoint("127.0.0.1", port), allowLoopback: true, CancellationToken.None);
        using var accepted = await acceptTask;
        Assert.That(stream.Socket.Connected, Is.True);
    }
}
