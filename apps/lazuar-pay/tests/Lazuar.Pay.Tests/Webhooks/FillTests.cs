using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class FillTests
{
    static string StripeSign(string secret, string payload, long t)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{t}.{payload}"));
        return $"t={t},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    static string StripePaid(string eventId, string checkoutId, string extra = "\"amount_total\":1000,\"currency\":\"myr\"") =>
        "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_x\",\"object\":\"checkout.session\",\"mode\":\"payment\"," + extra + ",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}}}";

    [Test]
    public async Task Fulfill_throw_returns_5xx_event_not_committed_retry_pays()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Probe.ThrowNext = true;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);

        var eventId = "evt_throw_" + Guid.NewGuid().ToString("N");
        var payload = StripePaid(eventId, checkoutId);
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", StripeSign(factory.StripeWebhookSecret, payload, t));
        var first = await client.SendAsync(req);
        Assert.That((int)first.StatusCode, Is.GreaterThanOrEqualTo(500));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.Documents.Count(), Is.EqualTo(0));
            Assert.That(db.PspWebhookEvents.Count(e => e.EventId == eventId), Is.EqualTo(0));
        }

        using var retry = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        retry.Headers.TryAddWithoutValidation("Stripe-Signature", StripeSign(factory.StripeWebhookSecret, payload, t));
        var second = await client.SendAsync(retry);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK), await second.Content.ReadAsStringAsync());
        using var after = factory.Services.CreateScope();
        Assert.That(after.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Amount_mismatch_does_not_mint_receipt()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);

        var eventId = "evt_mm_" + Guid.NewGuid().ToString("N");
        var payload = StripePaid(eventId, checkoutId, "\"amount_total\":999,\"currency\":\"myr\"");
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", StripeSign(factory.StripeWebhookSecret, payload, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var check = factory.Services.CreateScope();
        var db2 = check.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db2.Documents.Count(), Is.EqualTo(0));
        Assert.That(db2.PspWebhookEvents.Count(), Is.EqualTo(0));
        Assert.That(db2.Checkouts.Single().Status, Is.EqualTo("open"));
    }

    [Test]
    public async Task Currency_mismatch_does_not_mint_receipt()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);

        var eventId = "evt_ccy_" + Guid.NewGuid().ToString("N");
        var payload = StripePaid(eventId, checkoutId, "\"amount_total\":1000,\"currency\":\"usd\"");
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", StripeSign(factory.StripeWebhookSecret, payload, t));
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var check = factory.Services.CreateScope();
        Assert.That(check.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Rail_not_configured_is_400_when_body_present()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent("""{"id":"evt_x"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("rail not configured"));
    }

    [Test]
    public async Task Never_started_checkout_webhook_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single(x => x.Id == checkoutId).Provider = null;
            await db.SaveChangesAsync();
        }

        var eventId = "evt_nostart_" + Guid.NewGuid().ToString("N");
        var payload = StripePaid(eventId, checkoutId);
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", StripeSign(factory.StripeWebhookSecret, payload, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("provider mismatch"));
    }

    [Test]
    public async Task Empty_webhook_is_400()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent("", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Concurrent_fulfill_of_one_checkout_mints_one_receipt()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");

        using var scope = factory.Services.CreateScope();
        var fulfill = scope.ServiceProvider.GetRequiredService<IFulfillPaid>();
        await Task.WhenAll(
            fulfill.FulfillPaidAsync(checkoutId, "test", "ref-a", CancellationToken.None),
            fulfill.FulfillPaidAsync(checkoutId, "test", "ref-b", CancellationToken.None));
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Charges.Count(), Is.EqualTo(1));
        Assert.That(db.Checkouts.Single(x => x.Id == checkoutId).Status, Is.EqualTo("paid"));
    }

    [Test]
    public async Task Over_capacity_paid_webhook_books_pending_late_refund()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}""");
        var (linkToken, linkId) = await PayTest.SeedPaymentLink(client, "billplz", maxPayers: 2);
        var a = await PayTest.StartPay(client, linkToken, "slot-oc-a", """{"email":"ada@acme.test"}""");
        var b = await PayTest.StartPay(client, linkToken, "slot-oc-b", """{"email":"bob@acme.test"}""");
        Assert.That(a.IsSuccessStatusCode && b.IsSuccessStatusCode);

        string checkoutA, checkoutB;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            checkoutA = db.Checkouts.Single(x => x.SlotKey == "slot-oc-a").Id;
            checkoutB = db.Checkouts.Single(x => x.SlotKey == "slot-oc-b").Id;
        }

        var paidA = await PayAsync(WebhookForm(checkoutA, "bill_oc_a"));
        Assert.That(paidA.IsSuccessStatusCode, Is.True, await paidA.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var link = await db.PaymentLinks.SingleAsync(x => x.Id == linkId);
            link.MaxPayers = 1; // cap cut after A paid; B's money is already on the rail
            await db.SaveChangesAsync();
        }

        var paidB = await PayAsync(WebhookForm(checkoutB, "bill_oc_b"));
        Assert.That(paidB.IsSuccessStatusCode, Is.True, await paidB.Content.ReadAsStringAsync());

        using var after = factory.Services.CreateScope();
        var db2 = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db2.Checkouts.Single(x => x.Id == checkoutB).Status, Is.EqualTo("expired"));
        var refund = db2.Refunds.Single();
        Assert.That(refund.Status, Is.EqualTo("pending"));
        Assert.That(refund.Reason, Is.EqualTo("late_pay"));
        Assert.That(refund.CheckoutId, Is.EqualTo(checkoutB));
        // Billplz has no refund API: no PSP movement, no settlement, and the ops marker stays.
        Assert.That(db2.Charges.Count(), Is.EqualTo(1));
        Assert.That(db2.Documents.Count(), Is.EqualTo(1));

        async Task<HttpResponseMessage> PayAsync(string form)
        {
            using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1")
            {
                Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
            };
            return await client.SendAsync(wh);
        }
    }

    static string WebhookForm(string checkoutId, string billId)
    {
        var fields = Lazuar.Pay.Rails.Billplz.BillplzWebhook.ParseForm(
            $"id={billId}&paid=true&state=paid&paid_amount=1000&currency=MYR&x_signature=pending&reference_1={checkoutId}");
        var mac = Lazuar.Pay.Rails.Billplz.BillplzWebhook.ComputeHmac(fields, "xsig", excludeExtra: false);
        return $"id={billId}&paid=true&state=paid&paid_amount=1000&currency=MYR&x_signature={mac}&reference_1={checkoutId}";
    }
}
