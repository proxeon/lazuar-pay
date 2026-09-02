using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.PublicPay;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Regression tests for the PublicPay fixes (issues 007, 011, 016 in issues/001): the
/// double-mint race on /start, the slot-race recovery that kept a doomed INSERT tracked, and
/// the unbounded rate-limiter state.
/// </summary>
public class StartRaceTests
{
    [Test]
    public async Task Concurrent_starts_on_one_checkout_mint_exactly_one_session()
    {
        // Issue 007: two simultaneous starts both minted a hosted session and the second
        // write overwrote the first — the tab still displaying the first Solana QR could pay
        // on-chain into a reference nobody would ever confirm. Both requests must now share
        // ONE minted session.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var n = 0;
        factory.Psp.Responder = (_, _) =>
        {
            var seq = Interlocked.Increment(ref n);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    $$"""{"checkout_url":"https://pay.chip.test/{{seq}}","id":"pur_{{seq}}"}""",
                    Encoding.UTF8,
                    "application/json")
            };
        };
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        var (token, _) = await PayTest.SeedCheckout(client, "chip");

        var a = PayTest.StartPay(client, token, null, """{"name":"Ada","email":"ada@example.com"}""");
        var b = PayTest.StartPay(client, token, null, """{"name":"Ada","email":"ada@example.com"}""");
        var results = await Task.WhenAll(a, b);

        foreach (var r in results)
        {
            Assert.That(r.StatusCode, Is.EqualTo(HttpStatusCode.OK), await r.Content.ReadAsStringAsync());
            using var doc = JsonDocument.Parse(await r.Content.ReadAsStringAsync());
            Assert.That(doc.RootElement.GetProperty("redirect_url").GetString(),
                Is.EqualTo("https://pay.chip.test/1"), "both callers get the single minted session");
        }

        Assert.That(factory.Psp.SendCount, Is.EqualTo(1), "only one purchase may be created at CHIP");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<global::Lazuar.Pay.Data.PayDbContext>();
        Assert.That(db.Checkouts.Single().ProviderSessionId, Is.EqualTo("pur_1"));
    }

    [Test]
    public async Task Same_slot_start_race_recovers_the_loser_without_a_500()
    {
        // Issue 011: the loser of the (PaymentLinkId, SlotKey) insert race "recovered" the
        // winner's row, but the failed INSERT stayed tracked in the scoped context — the very
        // next SaveChanges re-attempted it and the payer got a spurious 500 and no pay URL.
        await using var factory = await PayPostgres.FactoryAsync();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, _) = await PayTest.SeedPaymentLink(client, maxPayers: 2);

        var a = PayTest.StartPay(client, token, "slot-race-same");
        var b = PayTest.StartPay(client, token, "slot-race-same");
        var results = await Task.WhenAll(a, b);

        // The tight race recovers the loser with 200 (same URL as the winner). A staggered
        // interleave — the test rail auto-fulfills, so the winner may already be "paid" when
        // the loser's slot query runs — legitimately answers the loser 409 "not open". Either
        // way: the payer gets a definitive answer and never a 5xx.
        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.OK), Is.GreaterThanOrEqualTo(1),
            await results[0].Content.ReadAsStringAsync() + await results[1].Content.ReadAsStringAsync());
        Assert.That(results.Count(r => (int)r.StatusCode >= 500), Is.EqualTo(0),
            "the insert-race loser must recover, not 500");
        Assert.That(results.Count(r => r.StatusCode == HttpStatusCode.OK), Is.LessThanOrEqualTo(2));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<global::Lazuar.Pay.Data.PayDbContext>();
        Assert.That(db.Checkouts.Count(x => x.SlotKey == "slot-race-same"), Is.EqualTo(1),
            "one checkout per slot, not two");
    }

    [Test]
    public void Limiter_sweeps_idle_keys_and_caps_key_length()
    {
        // Issue 016: unauthenticated route tokens keyed never-evicted entries — unbounded
        // memory on the public pay endpoints. Idle keys are now swept, and junk-length keys
        // are capped. (The limiter is process-static, so counts are relative.)
        var before = PublicPayLimiter.TrackedKeys;
        for (var i = 0; i < 100; i++)
        {
            Assert.That(PublicPayLimiter.TryAcquire("junk-" + i, max: 5, windowSeconds: 60), Is.True);
        }

        Assert.That(PublicPayLimiter.TrackedKeys, Is.EqualTo(before + 100));

        // A key whose hits are all older than the cutoff is evicted wholesale.
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        PublicPayLimiter.Sweep(now + 1);
        Assert.That(PublicPayLimiter.TrackedKeys, Is.EqualTo(0), "idle keys must not be retained forever");

        // Over-length keys are capped, not stored raw.
        var huge = new string('x', 10_000);
        Assert.That(PublicPayLimiter.TryAcquire(huge, max: 5, windowSeconds: 60), Is.True);
        Assert.That(PublicPayLimiter.TrackedKeys, Is.EqualTo(1));
    }
}
