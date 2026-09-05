using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Money;
using Lazuar.Pay.Rails;
using Microsoft.Extensions.DependencyInjection;
using Stripe;

namespace Lazuar.Pay.Tests;

/// <summary>
/// plans/031/02: the refund settle worker (stripe late_pay rows only, bounded inside the
/// 24 h idempotency-key window) and the writer resolve endpoint that closes everything the
/// worker must not touch (CHIP — no documented idempotency, rails without refund APIs,
/// rows past the window). Stripe HTTP is routed through the FakePspHandler via the
/// ProcessorRemote.StripeClientFactory test seam.
/// </summary>
public class RefundSettleTests
{
    static async Task PutHook(HttpClient client)
    {
        using var req = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/webhooks")
        {
            Content = new StringContent("""{"url":"http://127.0.0.1:9/hook"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(req)).IsSuccessStatusCode);
    }

    static async Task<string> SeedStripeLatePay(
        PayApiFactory factory,
        string checkoutId,
        int attemptCount = 1,
        DateTimeOffset? nextAttemptAt = null,
        DateTimeOffset? createdAt = null)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.Checkouts.Add(new CheckoutRow
        {
            Id = checkoutId, OrgId = "t1", PublicToken = "tok_" + checkoutId,
            Amount = 10m, Currency = "MYR", Status = "paid", Provider = "stripe",
            ProviderSessionId = "pi_" + checkoutId
        });
        db.Refunds.Add(new RefundRow
        {
            Id = "rf_" + checkoutId, OrgId = "t1", CheckoutId = checkoutId,
            Amount = 10m, Currency = "MYR", Status = "pending", Provider = "stripe",
            ProviderRef = "pi_" + checkoutId, Reason = "late_pay",
            AttemptCount = attemptCount, NextAttemptAt = nextAttemptAt,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return "rf_" + checkoutId;
    }

    static async Task MarkDueAsync(PayApiFactory factory, string refundId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.Refunds.Single(x => x.Id == refundId).NextAttemptAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }

    static async Task<RefundSettler> SettlerAsync(PayApiFactory factory)
    {
        var scope = factory.Services.CreateScope();
        var remote = scope.ServiceProvider.GetRequiredService<ProcessorRemote>();
        remote.StripeClientFactory = secret => new StripeClient(
            secret, httpClient: new SystemNetHttpClient(new HttpClient(factory.Psp), maxNetworkRetries: 0));
        return scope.ServiceProvider.GetRequiredService<RefundSettler>();
    }

    [Test]
    public async Task Worker_retries_until_settled_and_emits_refund_created()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PutHook(client);
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var refundId = await SeedStripeLatePay(factory, "c_rs1");

        var calls = 0;
        factory.Psp.Responder = (_, _) =>
        {
            calls++;
            return calls == 1
                ? new HttpResponseMessage(HttpStatusCode.InternalServerError)
                {
                    Content = new StringContent("""{"error":{"message":"boom"}}""", Encoding.UTF8, "application/json")
                }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("""{"id":"re_1","object":"refund","amount":1000,"status":"succeeded"}""", Encoding.UTF8, "application/json")
                };
        };

        using (var scope = factory.Services.CreateScope())
        {
            var settler = await SettlerAsync(factory);
            Assert.That(await settler.ProcessBatchAsync(CancellationToken.None), Is.EqualTo(1));
        }

        using (var after = factory.Services.CreateScope())
        {
            var db = after.ServiceProvider.GetRequiredService<PayDbContext>();
            var row = db.Refunds.Single(x => x.Id == refundId);
            Assert.That(row.Status, Is.EqualTo("pending"), "the 500 is ambiguous — stays pending");
            Assert.That(row.AttemptCount, Is.EqualTo(2));
            Assert.That(row.NextAttemptAt, Is.GreaterThan(DateTimeOffset.UtcNow));
            Assert.That(row.LastError, Is.EqualTo("settle outcome unknown"));
            Assert.That(factory.Psp.SendCount, Is.EqualTo(1));
            // Param reproduction (plans/031/02): same key, same params as the original ask.
            Assert.That(factory.Psp.LastRequest!.Headers.GetValues("Idempotency-Key").Single(),
                Is.EqualTo("lazuar-refund:" + refundId));
            Assert.That(factory.Psp.LastBody!, Does.Contain("amount=1000"));
            Assert.That(factory.Psp.LastBody!, Does.Contain("payment_intent=pi_c_rs1"));
            Assert.That(RefundMetrics.PendingStripeSnapshot, Is.GreaterThanOrEqualTo(1),
                "the batch publishes the pending-refund gauge");
        }

        await MarkDueAsync(factory, refundId);
        using (var scope2 = factory.Services.CreateScope())
        {
            var settler = await SettlerAsync(factory);
            await settler.ProcessBatchAsync(CancellationToken.None);
        }

        using (var after2 = factory.Services.CreateScope())
        {
            var db = after2.ServiceProvider.GetRequiredService<PayDbContext>();
            var row = db.Refunds.Single(x => x.Id == refundId);
            Assert.That(row.Status, Is.EqualTo("succeeded"));
            Assert.That(row.NextAttemptAt, Is.Null);
            Assert.That(factory.Psp.SendCount, Is.EqualTo(2), "one processor call per attempt");
            var delivery = db.OrgWebhookDeliveries.Single(x => x.EventType == "refund.created");
            Assert.That(delivery.EventId, Is.EqualTo(refundId));
            Assert.That(delivery.PayloadJson, Does.Contain("refund_id"));
        }
    }

    [Test]
    public async Task Worker_caps_attempts_and_never_claims_rows_past_the_key_window()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var refundId = await SeedStripeLatePay(factory, "c_cap");

        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":{"message":"boom"}}""", Encoding.UTF8, "application/json")
        };

        for (var i = 0; i < 5; i++)
        {
            await MarkDueAsync(factory, refundId);
            var settler = await SettlerAsync(factory);
            await settler.ProcessBatchAsync(CancellationToken.None);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var row = db.Refunds.Single(x => x.Id == refundId);
            Assert.That(row.AttemptCount, Is.EqualTo(6), "original + 5 worker retries");
            Assert.That(row.Status, Is.EqualTo("pending"), "never failed on an ambiguous outcome");
            Assert.That(factory.Psp.SendCount, Is.EqualTo(5));
        }

        var stalled = await SettlerAsync(factory);
        Assert.That(await stalled.ProcessBatchAsync(CancellationToken.None), Is.EqualTo(0),
            "the attempt cap must stop further processor calls");
        Assert.That(factory.Psp.SendCount, Is.EqualTo(5));

        // Past the idempotency window: never claimed — the key may already be pruned.
        await using var factory2 = new PayApiFactory();
        factory2.One.Responder = PayTest.Owner;
        await SeedStripeLatePay(factory2, "c_old", createdAt: DateTimeOffset.UtcNow.AddHours(-25));
        var settler2 = await SettlerAsync(factory2);
        Assert.That(await settler2.ProcessBatchAsync(CancellationToken.None), Is.EqualTo(0));
        Assert.That(factory2.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Original_settle_failure_schedules_the_first_worker_retry()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var refundId = await SeedStripeLatePay(factory, "c_sched", attemptCount: 0);
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("""{"error":{"message":"boom"}}""", Encoding.UTF8, "application/json")
        };

        using var scope = factory.Services.CreateScope();
        var remote = scope.ServiceProvider.GetRequiredService<ProcessorRemote>();
        remote.StripeClientFactory = secret => new StripeClient(
            secret, httpClient: new SystemNetHttpClient(new HttpClient(factory.Psp), maxNetworkRetries: 0));
        var checkout = scope.ServiceProvider.GetRequiredService<PayDbContext>().Checkouts.Single();
        var settled = await remote.SettlePendingRefundAsync(refundId, checkout, "pi_c_sched", 1000, CancellationToken.None);

        Assert.That(settled, Is.False);
        var row = scope.ServiceProvider.GetRequiredService<PayDbContext>().Refunds.Single(x => x.Id == refundId);
        Assert.That(row.AttemptCount, Is.EqualTo(1), "the original attempt counts as attempt 1");
        Assert.That(row.NextAttemptAt, Is.GreaterThan(DateTimeOffset.UtcNow));
        Assert.That(row.NextAttemptAt, Is.LessThan(DateTimeOffset.UtcNow.AddMinutes(2)),
            "first retry ~1m later — the whole schedule must stay inside the 24 h key window");
    }

    [Test]
    public async Task Resolve_closes_a_pending_refund_and_emits_the_webhook()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PutHook(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Refunds.Add(new RefundRow
            {
                Id = "rf_manual", OrgId = "t1", CheckoutId = "c_manual",
                Amount = 10m, Currency = "MYR", Status = "pending", Provider = "billplz",
                Reason = "late_pay", CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds/rf_manual/resolve")
        {
            Content = new StringContent("""{"status":"succeeded"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());

        using (var after = factory.Services.CreateScope())
        {
            var db = after.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.Refunds.Single().Status, Is.EqualTo("succeeded"));
            var delivery = db.OrgWebhookDeliveries.Single(x => x.EventType == "refund.created");
            Assert.That(delivery.EventId, Is.EqualTo("rf_manual"));
            Assert.That(db.AuditEvents.Any(x => x.Action == "refund.resolved"), Is.True);
        }

        using var again = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds/rf_manual/resolve")
        {
            Content = new StringContent("""{"status":"succeeded"}""", Encoding.UTF8, "application/json")
        };
        again.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(again)).StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Resolve_rejects_bad_status_member_lease_and_unknown_ids()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Refunds.Add(new RefundRow
            {
                Id = "rf_guard", OrgId = "t1", CheckoutId = "c_guard1",
                Amount = 10m, Currency = "MYR", Status = "pending", Provider = "billplz",
                Reason = "late_pay", CreatedAt = DateTimeOffset.UtcNow
            });
            db.Refunds.Add(new RefundRow
            {
                Id = "rf_lease", OrgId = "t1", CheckoutId = "c_guard2",
                Amount = 10m, Currency = "MYR", Status = "pending", Provider = "stripe",
                Reason = "late_pay", AttemptCount = 1,
                NextAttemptAt = DateTimeOffset.UtcNow.AddSeconds(30),
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        async Task<HttpResponseMessage> Resolve(string refundId, string json)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/orgs/t1/refunds/{refundId}/resolve")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
            req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
            return await client.SendAsync(req);
        }

        Assert.That((await Resolve("rf_guard", """{"status":"nope"}""")).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That((await Resolve("rf_missing", """{"status":"succeeded"}""")).StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That((await Resolve("rf_lease", """{"status":"failed"}""")).StatusCode, Is.EqualTo(HttpStatusCode.Conflict),
            "a row inside the worker's claim lease must not be resolved underneath it");
        var failed = await Resolve("rf_guard", """{"status":"failed"}""");
        Assert.That(failed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Refunds.Single(x => x.Id == "rf_guard").Status, Is.EqualTo("failed"));
        Assert.That(pay.OrgWebhookDeliveries.Count(x => x.EventType == "refund.created"), Is.EqualTo(0),
            "a failed resolve must not emit refund.created");
    }

    [Test]
    public async Task Chip_refunds_carry_the_idempotency_header()
    {
        // plans/031/02 step 0: the header is sent even though CHIP's refund docs do not
        // document idempotency — harmless, and the settle worker still never claims CHIP.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"pay_re1","object":"payment"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.PutChip(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Add(new CheckoutRow
            {
                Id = "c_chip", OrgId = "t1", PublicToken = "tok_c_chip",
                Amount = 10m, Currency = "MYR", Status = "paid", Provider = "chip",
                ProviderSessionId = "pur_c_chip"
            });
            db.Charges.Add(new ChargeRow
            {
                Id = "ch_chip", OrgId = "t1", CheckoutId = "c_chip",
                Provider = "chip", ProviderRef = "pur_c_chip",
                Amount = 10m, Currency = "MYR", Status = "paid"
            });
            await db.SaveChangesAsync();
        }

        using var refund = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent("""{"checkout_id":"c_chip"}""", Encoding.UTF8, "application/json")
        };
        refund.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        refund.Headers.TryAddWithoutValidation("Idempotency-Key", "rk-chip");
        var response = await client.SendAsync(refund);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var refundId = doc.RootElement.GetProperty("id").GetString();

        Assert.That(factory.Psp.LastRequest!.Headers.GetValues("Idempotency-Key").Single(),
            Is.EqualTo("lazuar-refund:" + refundId));
    }
}
