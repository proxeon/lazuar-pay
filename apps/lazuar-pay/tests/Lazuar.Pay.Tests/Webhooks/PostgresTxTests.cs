using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class PostgresTxTests
{
    static string StripeSign(string secret, string payload, long t)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{t}.{payload}"));
        return $"t={t},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    static string StripePaid(string eventId, string checkoutId) =>
        "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_x\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}}}";

    [Test]
    public async Task Fulfill_save_then_throw_rolls_back_event()
    {
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        factory.Probe.ThrowAfterSave = true;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);

        var eventId = "evt_tx_" + Guid.NewGuid().ToString("N");
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
            Assert.That(await db.Documents.CountAsync(), Is.EqualTo(0));
            Assert.That(await db.PspWebhookEvents.CountAsync(e => e.EventId == eventId), Is.EqualTo(0));
        }

        using var retry = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        retry.Headers.TryAddWithoutValidation("Stripe-Signature", StripeSign(factory.StripeWebhookSecret, payload, t));
        var second = await client.SendAsync(retry);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK), await second.Content.ReadAsStringAsync());
        using var after = factory.Services.CreateScope();
        var paid = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(await paid.Documents.CountAsync(), Is.EqualTo(1));
        Assert.That(await paid.PspWebhookEvents.CountAsync(e => e.EventId == eventId), Is.EqualTo(1));
    }

    [Test]
    public async Task Concurrent_starts_on_one_seat_leave_one_open()
    {
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 1);

        var a = PayTest.StartPay(client, token, "slot-pg-a");
        var b = PayTest.StartPay(client, token, "slot-pg-b");
        var results = await Task.WhenAll(a, b);
        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(results.Count(r => (int)r.StatusCode is >= 400 and < 500), Is.EqualTo(1));

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/payment-links");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var listed = await client.SendAsync(list);
        using var doc = JsonDocument.Parse(await listed.Content.ReadAsStringAsync());
        var items = PayTest.Items(doc.RootElement);
        Assert.That(items[0].GetProperty("taken_count").GetInt32(), Is.EqualTo(1));
        Assert.That(items[0].GetProperty("status").GetString(), Is.EqualTo("full"));
    }

    [Test]
    public async Task Concurrent_fulfill_same_checkout_one_receipt()
    {
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);

        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        async Task<HttpResponseMessage> Post(string eventId)
        {
            var payload = StripePaid(eventId, checkoutId);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Stripe-Signature", StripeSign(factory.StripeWebhookSecret, payload, t));
            return await client.SendAsync(req);
        }

        var results = await Task.WhenAll(
            Post("evt_rcpt_a_" + Guid.NewGuid().ToString("N")),
            Post("evt_rcpt_b_" + Guid.NewGuid().ToString("N")));
        Assert.That(results.All(r => r.IsSuccessStatusCode), Is.True,
            string.Join(" | ", results.Select(r => r.StatusCode + " " + r.Content.ReadAsStringAsync().Result)));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(await db.Documents.CountAsync(), Is.EqualTo(1));
        Assert.That(await db.Charges.CountAsync(), Is.EqualTo(1));
    }
}
