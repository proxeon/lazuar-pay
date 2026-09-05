using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class RazorpayRailTests
{
    [Test]
    public async Task Razorpay_captured()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());

        var payload = "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"amount\":1000,\"currency\":\"INR\",\"tax\":12,\"fee\":30,\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}}}}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        var sig = Convert.ToHexString(mac).ToLowerInvariant();
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", sig);
        var paid = await client.SendAsync(wh);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
        Assert.That(db.JournalLines.Count(), Is.EqualTo(2));
        Assert.That(db.JournalLines.Where(l => l.Dc == "D").Sum(l => l.Amount), Is.EqualTo(db.JournalLines.Where(l => l.Dc == "C").Sum(l => l.Amount)));
    }

    [Test]
    public async Task Razorpay_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}""");
        var (token, _) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Razorpay_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}""");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Razorpay_link_paid_fulfills_and_replays_dedupe()
    {
        // Issue 004 (issues/003): Pay mints payment links, and Razorpay's payment-links
        // docs point merchants at payment_link.paid — the event Pay used to file under
        // "ignored", so a merchant subscribing to exactly that event never got a
        // fulfillment. It must fulfill, and a redelivery must dedupe.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).IsSuccessStatusCode);

        var payload = "{\"event\":\"payment_link.paid\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_lp\",\"amount\":1000,\"currency\":\"INR\",\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}},\"payment_link\":{\"entity\":{\"id\":\"plink_1\",\"status\":\"paid\"}}}}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var paid = await client.SendAsync(wh);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        Assert.That(await paid.Content.ReadAsStringAsync(), Does.Contain("ok"));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
            Assert.That(db.Charges.Single().ProviderRef, Is.EqualTo("pay_lp"));
            Assert.That(db.Charges.Single().Currency, Is.EqualTo("INR"));
            Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
            Assert.That(db.PspWebhookEvents.Single().EventId, Is.EqualTo("link_paid:pay_lp"));
        }

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        replay.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var again = await client.SendAsync(replay);
        Assert.That(await again.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        using var after = factory.Services.CreateScope();
        Assert.That(after.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Razorpay_link_paid_without_notes_joins_plink()
    {
        // payment_link.paid carries payment_link.entity.id — binding must also work through
        // ProviderSessionId when the payment entity has no notes object.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, _) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = """{"event":"payment_link.paid","payload":{"payment":{"entity":{"id":"pay_ln","amount":1000,"currency":"INR"}},"payment_link":{"entity":{"id":"plink_1"}}}}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var paid = await client.SendAsync(wh);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
        Assert.That(db.Charges.Single().ProviderRef, Is.EqualTo("pay_ln"));
    }

    [Test]
    public async Task Razorpay_captured_then_link_paid_books_one_charge()
    {
        // Merchants who enable both events get two deliveries for one payment. The first
        // fulfills; the second (distinct event id, so no dedupe) must be answered ok
        // without booking anything a second time.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);

        async Task<HttpResponseMessage> Send(string eventType, string payId)
        {
            var payload = "{\"event\":\"" + eventType + "\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"" + payId + "\",\"amount\":1000,\"currency\":\"INR\",\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}},\"payment_link\":{\"entity\":{\"id\":\"plink_1\",\"status\":\"paid\"}}}}";
            var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
            using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
            return await client.SendAsync(wh);
        }

        var captured = await Send("payment.captured", "pay_both");
        Assert.That(captured.StatusCode, Is.EqualTo(HttpStatusCode.OK), await captured.Content.ReadAsStringAsync());
        var linkPaid = await Send("payment_link.paid", "pay_both");
        Assert.That(linkPaid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await linkPaid.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Charges.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
    }

    [Test]
    public async Task Razorpay_link_expired_is_ignored()
    {
        // Expiry stays local: the TTL sweep owns it, and ignoring keeps the late-pay route
        // reachable for a capture that trails an expired link.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, _) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = """{"event":"payment_link.expired","payload":{"payment_link":{"entity":{"id":"plink_1","status":"expired"}}}}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("payment_link.expired"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
        Assert.That(db.Charges.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Razorpay_payment_failed_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = "{\"event\":\"payment.failed\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"amount\":1000,\"currency\":\"INR\",\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}}}}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("failed"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Razorpay_captured_without_notes_joins_plink()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, _) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = """{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":1000,"currency":"INR"}},"payment_link":{"entity":{"id":"plink_1"}}}}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var paid = await client.SendAsync(wh);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }
}
