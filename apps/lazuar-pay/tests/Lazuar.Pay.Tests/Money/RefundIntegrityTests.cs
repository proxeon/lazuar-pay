using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Rails;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Regression tests for the refund-integrity fixes (issues 001, 009, 010, 012 in
/// issues/001; 001 in issues/003): ambiguous processor outcomes, one late-pay refund per
/// checkout, settle-time status recompute, the same-key partial-refund race, and the
/// zero-minor-amount guard that keeps an amount-less Stripe refund unreachable.
/// </summary>
public class RefundIntegrityTests
{
    /// <summary>Seed a paid chip checkout + charge directly so the refund path has a session to hit.</summary>
    static async Task<string> SeedPaidChipCheckout(PayApiFactory factory, string checkoutId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.Checkouts.Add(new CheckoutRow
        {
            Id = checkoutId, OrgId = "t1", PublicToken = "tok_" + checkoutId,
            Amount = 10m, Currency = "MYR", Status = "paid", Provider = "chip",
            ProviderSessionId = "pur_" + checkoutId
        });
        db.Charges.Add(new ChargeRow
        {
            Id = "ch_" + checkoutId, OrgId = "t1", CheckoutId = checkoutId,
            Provider = "chip", ProviderRef = "pur_" + checkoutId,
            Amount = 10m, Currency = "MYR", Status = "paid"
        });
        await db.SaveChangesAsync();
        return checkoutId;
    }

    static async Task<HttpResponseMessage> Refund(HttpClient client, string checkoutId, string? key, string? json = null)
    {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent(json ?? $$"""{"checkout_id":"{{checkoutId}}"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        if (key is not null)
        {
            req.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        }

        return await client.SendAsync(req);
    }

    [Test]
    public async Task Ambiguous_refund_outcome_keeps_reservation_pending_instead_of_releasing_it()
    {
        // Issue 001: a lost response after CHIP may have executed the refund must not book
        // "failed" — that released the refundable remainder and a retry refunded twice.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => throw new HttpRequestException("connection reset by peer");
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var checkoutId = await SeedPaidChipCheckout(factory, "c_amb");

        var first = await Refund(client, checkoutId, "amb-1");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway), await first.Content.ReadAsStringAsync());
        Assert.That(await first.Content.ReadAsStringAsync(), Does.Contain("held pending"));

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.Refunds.Single().Status, Is.EqualTo("pending"), "ambiguous outcome must hold the reservation");
            Assert.That(db.Charges.Single().Status, Is.EqualTo("paid"));
        }

        // Same-key retry replays the pending row — never re-attempts blind, never 500s.
        var retry = await Refund(client, checkoutId, "amb-1");
        Assert.That(retry.StatusCode, Is.EqualTo(HttpStatusCode.OK), await retry.Content.ReadAsStringAsync());
        using var replayDoc = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.That(replayDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("pending"));

        // A refund with a different key is blocked: the pending row still reserves capacity.
        var other = await Refund(client, checkoutId, "amb-2");
        Assert.That(other.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await other.Content.ReadAsStringAsync());

        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Refunds.Count(), Is.EqualTo(1), "no second refund row may be minted while the first is pending");
    }

    [Test]
    public async Task Definitive_rejection_releases_the_reservation_so_a_retry_can_succeed()
    {
        // Issue 001 (the other half): a definitive processor no (4xx) provably moved no money,
        // so releasing the reservation there is safe and a retry with a new key succeeds.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.UnprocessableEntity);
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var checkoutId = await SeedPaidChipCheckout(factory, "c_rej");

        var first = await Refund(client, checkoutId, "rej-1");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.BadGateway), await first.Content.ReadAsStringAsync());

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.Refunds.Single().Status, Is.EqualTo("failed"), "a definitive no must release the reservation");
        }

        // The processor heals; the retry (new key, since the old one is spent on the failed row) succeeds.
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK);
        var second = await Refund(client, checkoutId, "rej-2");
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created), await second.Content.ReadAsStringAsync());

        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Refunds.Single(x => x.IdempotencyKey == "rej-2").Status, Is.EqualTo("succeeded"));
        Assert.That(pay.Charges.Single().Status, Is.EqualTo("refunded"));
    }

    [Test]
    public async Task Second_late_pay_event_for_one_checkout_books_exactly_one_refund()
    {
        // Issue 009: Stripe async payments deliver two success events with distinct event ids
        // (async_payment_succeeded + checkout.session.completed). The second used to book a
        // second refund row that could never settle.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single(x => x.Id == checkoutId).Status = "expired";
            await db.SaveChangesAsync();
        }

        async Task<HttpResponseMessage> LateEvent(string eventId)
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

        var first = await LateEvent("evt_dbl_1");
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), await first.Content.ReadAsStringAsync());
        var second = await LateEvent("evt_dbl_2");
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK), await second.Content.ReadAsStringAsync());

        using var scope2 = factory.Services.CreateScope();
        var pay = scope2.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.PspWebhookEvents.Count(x => x.Provider == "test"), Is.EqualTo(2), "both events are still recorded");
        Assert.That(pay.Refunds.Count(), Is.EqualTo(1), "one late_pay refund per checkout");
        Assert.That(pay.Refunds.Single().Reason, Is.EqualTo("late_pay"));
        Assert.That(pay.Refunds.Single().Status, Is.EqualTo("succeeded"));
    }

    [Test]
    public async Task Concurrent_same_key_partial_refunds_answer_as_replay_not_500()
    {
        // Issue 012: the loser of the (OrgId, IdempotencyKey) insert race used to surface a
        // raw 500; it is a replay by contract.
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await SeedPaidTestCheckout(factory, "c_race");

        var a = Refund(client, "c_race", "race-1", """{"checkout_id":"c_race","amount":4}""");
        var b = Refund(client, "c_race", "race-1", """{"checkout_id":"c_race","amount":4}""");
        var results = await Task.WhenAll(a, b);

        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.Created), Is.EqualTo(1),
            await results[0].Content.ReadAsStringAsync() + await results[1].Content.ReadAsStringAsync());
        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.OK), Is.EqualTo(1));
        Assert.That(results.Count(r => (int)r.StatusCode >= 500), Is.EqualTo(0), "no raw 500s on the insert race");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Refunds.Count(), Is.EqualTo(1));
        Assert.That(db.Refunds.Single().Amount, Is.EqualTo(4m));
        Assert.That(db.Refunds.Single().Status, Is.EqualTo("succeeded"));
        Assert.That(db.Charges.Single().Status, Is.EqualTo("partially_refunded"));
    }

    [Test]
    public async Task Concurrent_partial_refunds_leave_fully_refunded_charge_labeled_refunded()
    {
        // Issue 010: the status write used the reserve-time remaining snapshot with no lock,
        // so the 60-refund's stale "partially_refunded" could land after the final refund's
        // "refunded" and mislabel the charge forever.
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await SeedPaidTestCheckout(factory, "c_status");

        var a = Refund(client, "c_status", "st-1", """{"checkout_id":"c_status","amount":4}""");
        var b = Refund(client, "c_status", "st-2", """{"checkout_id":"c_status","amount":6}""");
        var results = await Task.WhenAll(a, b);
        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.Created), Is.EqualTo(2),
            await results[0].Content.ReadAsStringAsync() + await results[1].Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Refunds.Count(x => x.Status == "succeeded"), Is.EqualTo(2));
        Assert.That(db.Charges.Single().Status, Is.EqualTo("refunded"),
            "the last status write must be recomputed from persisted rows under the charge lock");
    }

    [Test]
    public async Task Stripe_refund_of_a_zero_minor_amount_throws_instead_of_omitting_the_amount()
    {
        // Issue 001 (issues/003): RefundStripeAsync treated ToMinor(0.002)==0 as "no partial
        // amount supplied" — which is Stripe's refund-everything default. A supplied amount
        // of zero must be a definite precondition failure (ProcessorRejectedException), so
        // the merchant path can never full-refund on a sub-cent ask. The pi_ session id lets
        // the guard fire before any processor round-trip.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Add(new CheckoutRow
            {
                Id = "c_pi", OrgId = "t1", PublicToken = "tok_c_pi",
                Amount = 10m, Currency = "MYR", Status = "paid", Provider = "stripe",
                ProviderSessionId = "pi_test_1"
            });
            db.Charges.Add(new ChargeRow
            {
                Id = "ch_pi", OrgId = "t1", CheckoutId = "c_pi",
                Provider = "stripe", ProviderRef = "pi_test_1",
                Amount = 10m, Currency = "MYR", Status = "paid"
            });
            await db.SaveChangesAsync();

            var remote = scope.ServiceProvider.GetRequiredService<ProcessorRemote>();
            var checkout = db.Checkouts.Single(x => x.Id == "c_pi");
            var charge = db.Charges.Single(x => x.Id == "ch_pi");
            Assert.ThrowsAsync<ProcessorRejectedException>(
                async () => await remote.RefundChargeAsync(charge, checkout, 0.002m, "ref_zero", CancellationToken.None));
            Assert.ThrowsAsync<ProcessorRejectedException>(
                async () => await remote.RefundChargeAsync(charge, checkout, -1m, "ref_negative", CancellationToken.None));
        }

        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Refunds.Count(), Is.EqualTo(0), "the guard fires before any row can book");
    }

    [Test]
    public async Task Paused_org_still_books_and_settles_the_late_pay_refund_on_an_expired_checkout()
    {
        // Issue 002 (issues/003): the charges-paused 409 used to fire before the late-pay
        // branch, so a capture that arrived for a suspended org booked nothing — no refund
        // row, no event, nothing to reconcile on reactivation. Pausing stops NEW charges;
        // returning money is bookkeeping and must still run.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single(x => x.Id == checkoutId).Status = "expired";
            db.OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused = true;
            await db.SaveChangesAsync();
        }

        var body = $$"""{"id":"evt_paused_late","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());

        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Refunds.Single().Reason, Is.EqualTo("late_pay"));
        Assert.That(pay.Refunds.Single().Status, Is.EqualTo("succeeded"), "the test rail settles instantly");
        Assert.That(pay.PspWebhookEvents.Count(x => x.Provider == "test"), Is.EqualTo(1));
        Assert.That(pay.Checkouts.Single().Status, Is.EqualTo("expired"));
        Assert.That(pay.Charges.Count(), Is.EqualTo(0), "paused org must not gain a fulfilled charge");
        Assert.That(pay.Documents.Count(), Is.EqualTo(0));
    }

    /// <summary>Paid checkout + charge on the no-op test rail (refund "executes" instantly).</summary>
    static async Task SeedPaidTestCheckout(PayApiFactory factory, string checkoutId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.Checkouts.Add(new CheckoutRow
        {
            Id = checkoutId, OrgId = "t1", PublicToken = "tok_" + checkoutId,
            Amount = 10m, Currency = "MYR", Status = "paid", Provider = "test"
        });
        db.Charges.Add(new ChargeRow
        {
            Id = "ch_" + checkoutId, OrgId = "t1", CheckoutId = checkoutId,
            Provider = "test", ProviderRef = "re_" + checkoutId,
            Amount = 10m, Currency = "MYR", Status = "paid"
        });
        await db.SaveChangesAsync();
    }
}
