using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.PaymentLinks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Regression tests for the occupancy/expiry races (issues 002 and 008 in issues/001):
/// compare-and-set status transitions so the expiry sweep cannot overwrite a committed
/// "paid" (and vice versa), and the parent-link lock at fulfillment so concurrent late
/// captures cannot both admit on a full link.
/// </summary>
public class OccupancyRaceTests
{
    static async Task SeedLink(PayApiFactory factory, string linkId, string token, int? maxPayers)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.PaymentLinks.Add(new PaymentLinkRow
        {
            Id = linkId, OrgId = "t1", PublicToken = token, Provider = "test",
            Amount = 10m, Currency = "MYR", MaxPayers = maxPayers, CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    static async Task SeedChild(
        PayApiFactory factory, string id, string linkId, string slot,
        DateTimeOffset createdAt, string status = "open")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.Checkouts.Add(new CheckoutRow
        {
            Id = id, OrgId = "t1", PublicToken = "tok_" + id, PaymentLinkId = linkId, SlotKey = slot,
            Amount = 10m, Currency = "MYR", Status = status, Provider = "test",
            Interval = "one_off", CreatedAt = createdAt
        });
        await db.SaveChangesAsync();
    }

    static async Task<HttpResponseMessage> TestWebhookPaid(HttpClient client, string eventId, string checkoutId)
    {
        var body = $$"""{"id":"{{eventId}}","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        return await client.SendAsync(req);
    }

    [Test]
    public async Task Sweep_cannot_overwrite_a_committed_paid_checkout()
    {
        // Issue 002: the sweep SELECTs stale open rows, then writes. A payment committing in
        // between used to be blindly overwritten to "expired" — capacity freed for a delivered
        // order. Both layers are pinned: the fresh re-query must not select the paid row, and
        // the write itself (driven with the stale row list the sweep would hold) must refuse.
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await SeedLink(factory, "lk_002", "tok_lk_002", maxPayers: 1);
        await SeedChild(factory, "c_002", "lk_002", "slot-002-aaaa", DateTimeOffset.UtcNow.AddMinutes(-31));

        // The payment lands and commits while a hypothetical sweep is in flight.
        var paid = await TestWebhookPaid(client, "evt_002_pay", "c_002");
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();

            // Layer 1 — the sweep's fresh SELECT does not see the paid row as stale-open.
            var expired = await PaymentLinkOccupancy.ExpireStaleAsync(
                db, "lk_002", TimeSpan.FromMinutes(30), CancellationToken.None);
            Assert.That(expired.Count, Is.EqualTo(0), "a paid checkout must not be expired by the sweep");

            // Layer 2 — even the stale row list the sweep SELECTed before the payment
            // committed must fail to flip the row: the CAS sees Status != 'open'.
            var staleOpenView = new CheckoutRow
            {
                Id = "c_002", OrgId = "t1", PublicToken = "tok_c_002", PaymentLinkId = "lk_002",
                SlotKey = "slot-002-aaaa", Amount = 10m, Currency = "MYR", Status = "open",
                Provider = "test", Interval = "one_off", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-31)
            };
            var marked = await PaymentLinkOccupancy.MarkExpiredAsync(
                db, [staleOpenView], "ttl", CancellationToken.None);
            Assert.That(marked.Count, Is.EqualTo(0), "the CAS write must refuse a row that left 'open'");

            var fresh = await db.Checkouts.AsNoTracking().SingleAsync(x => x.Id == "c_002");
            Assert.That(fresh.Status, Is.EqualTo("paid"));
            Assert.That(await db.Charges.CountAsync(x => x.CheckoutId == "c_002"), Is.EqualTo(1));
            Assert.That(await db.OrgWebhookDeliveries.CountAsync(x => x.EventType == "checkout.expired"),
                Is.EqualTo(0), "no expiry webhook may fire for a fulfilled checkout");
        }
    }

    [Test]
    public async Task Concurrent_late_captures_on_a_full_link_admit_one_and_refund_the_loser()
    {
        // Issue 008: the over-capacity check counted paid rows without the parent-link lock,
        // so two concurrent late captures on a full link both passed and both were kept, with
        // no refund for the excess.
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await SeedLink(factory, "lk_008", "tok_lk_008", maxPayers: 1);
        var stale = DateTimeOffset.UtcNow.AddMinutes(-31);
        await SeedChild(factory, "c_008_a", "lk_008", "slot-008-aaaa", stale);
        await SeedChild(factory, "c_008_b", "lk_008", "slot-008-bbbb", stale);

        var a = TestWebhookPaid(client, "evt_008_a", "c_008_a");
        var b = TestWebhookPaid(client, "evt_008_b", "c_008_b");
        var results = await Task.WhenAll(a, b);
        foreach (var r in results)
        {
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
        }

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var statuses = await db.Checkouts
            .Where(x => x.PaymentLinkId == "lk_008")
            .ToDictionaryAsync(x => x.Id, x => x.Status);
        Assert.That(statuses.Values.Count(s => s == "paid"), Is.EqualTo(1), "only one payer may be admitted");
        Assert.That(statuses.Values.Count(s => s == "expired"), Is.EqualTo(1), "the loser is expired, not silently dropped");
        Assert.That(await db.Charges.CountAsync(x => x.CheckoutId == "c_008_a" || x.CheckoutId == "c_008_b"),
            Is.EqualTo(1), "exactly one charge for the admitted payer");
        var lateRefund = await db.Refunds.SingleAsync(x => x.Reason == "late_pay");
        Assert.That(lateRefund.Status, Is.EqualTo("succeeded"), "the test rail settles the late refund");
    }

    [Test]
    public async Task Sweep_expires_stale_open_rows_and_notifies()
    {
        // Guard rail for the CAS change: the happy path (a genuinely stale open row) still
        // expires and fires the webhook.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var hook = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/webhooks")
        {
            Content = new StringContent("""{"url":"http://127.0.0.1:9/hook"}""", Encoding.UTF8, "application/json")
        };
        hook.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(hook)).IsSuccessStatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.PaymentLinks.Add(new PaymentLinkRow
        {
            Id = "lk_stale", OrgId = "t1", PublicToken = "tok_stale", Provider = "test",
            Amount = 10m, Currency = "MYR", MaxPayers = 1, CreatedAt = DateTimeOffset.UtcNow
        });
        db.Checkouts.Add(new CheckoutRow
        {
            Id = "c_stale", OrgId = "t1", PublicToken = "tok_c_stale", PaymentLinkId = "lk_stale",
            SlotKey = "slot-stale-aaaa", Amount = 10m, Currency = "MYR", Status = "open",
            Provider = "test", Interval = "one_off", CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-31)
        });
        await db.SaveChangesAsync();

        var expired = await PaymentLinkOccupancy.ExpireStaleAsync(
            db, "lk_stale", TimeSpan.FromMinutes(30), CancellationToken.None);
        Assert.That(expired.Count, Is.EqualTo(1));
        var row = await db.Checkouts.AsNoTracking().SingleAsync(x => x.Id == "c_stale");
        Assert.That(row.Status, Is.EqualTo("expired"));
        Assert.That(await db.OrgWebhookDeliveries.CountAsync(x => x.EventType == "checkout.expired"), Is.EqualTo(1));
    }
}
