using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class FailedAndExpiredTests
{
    [Test]
    public async Task Test_failed_webhook_persists_and_enqueues()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PutHook(client);
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        var body = $$"""{"id":"evt_fail","checkout_id":"{{checkoutId}}","status":"failed","currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("failed"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("failed"));
        Assert.That(db.Documents.Count(), Is.EqualTo(0));
        Assert.That(db.OrgWebhookDeliveries.Single().EventType, Is.EqualTo("payment.failed"));
    }

    [Test]
    public async Task Ignored_psp_does_not_emit_failed()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = pem, public_merchant_id = "brand_1" }));
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "chip");
        var payload = "{\"event_type\":\"purchase.preauthorized\",\"id\":\"purch_1\",\"purchase\":{\"id\":\"purch_1\",\"total\":0,\"currency\":\"MYR\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}}";
        var sig = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/chip/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Signature", sig);
        var response = await client.SendAsync(wh);
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("preauthorized"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
        Assert.That(db.OrgWebhookDeliveries.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Paused_org_still_records_failed_events_and_marks_the_subscription_past_due()
    {
        // Issue 002 (issues/003): the paused 409 used to swallow payment_failed before the
        // dedupe row was written — the event vanished on every PSP retry and the
        // subscription never moved to past_due. Recording a failure is bookkeeping, not
        // charging; only fulfillment stays blocked while paused.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PutHook(client);
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"test"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var createdDoc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var checkoutId = createdDoc.RootElement.GetProperty("id").GetString()!;

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused = true;
            await db.SaveChangesAsync();
        }

        var body = $$"""{"id":"evt_paused_fail","checkout_id":"{{checkoutId}}","status":"failed","currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("failed"));

        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Checkouts.Single().Status, Is.EqualTo("failed"));
        Assert.That(pay.PspWebhookEvents.Single(x => x.Provider == "test").EventId, Is.EqualTo("evt_paused_fail"));
        // plans/031/01 (Option A): no dunning bookkeeping — no subscription row exists to
        // flip past_due, and the merchant's own failed webhook is the signal.
        Assert.That(pay.Subscriptions.Count(), Is.EqualTo(0));
        Assert.That(pay.OrgWebhookDeliveries.Single().EventType, Is.EqualTo("payment.failed"));
    }

    [Test]
    public async Task Stale_reservation_emits_checkout_expired()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PutHook(client);
        var (token, linkId) = await PayTest.SeedPaymentLink(client, maxPayers: 1);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var link = db.PaymentLinks.Single(x => x.Id == linkId || x.PublicToken == token);
            db.Checkouts.Add(new CheckoutRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = link.OrgId,
                Provider = link.Provider,
                PaymentLinkId = link.Id,
                SlotKey = "slot-exp-1",
                PublicToken = Convert.ToHexString(Guid.NewGuid().ToByteArray()),
                Amount = link.Amount,
                Currency = link.Currency,
                Status = "open",
                Interval = "one_off",
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-31)
            });
            await db.SaveChangesAsync();
        }

        var get = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK), await get.Content.ReadAsStringAsync());
        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Checkouts.Single(x => x.PaymentLinkId != null).Status, Is.EqualTo("expired"));
        Assert.That(pay.OrgWebhookDeliveries.Single().EventType, Is.EqualTo("checkout.expired"));
    }

    static async Task PutHook(HttpClient client)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/webhooks")
        {
            Content = new StringContent("""{"url":"http://127.0.0.1:9/hook"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(req)).IsSuccessStatusCode);
    }
}
