using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class RefundTests
{
    [Test]
    public async Task Full_refund_reverses_journal_and_uses_ref_number()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var hook = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/webhooks")
        {
            Content = new StringContent("""{"url":"http://127.0.0.1:9/hook"}""", Encoding.UTF8, "application/json")
        };
        hook.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(hook)).IsSuccessStatusCode);
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var refund = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent($$"""{"checkout_id":"{{checkoutId}}"}""", Encoding.UTF8, "application/json")
        };
        refund.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        refund.Headers.TryAddWithoutValidation("Idempotency-Key", "ref-1");
        var response = await client.SendAsync(refund);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("succeeded"));
        Assert.That(doc.RootElement.GetProperty("number").GetString(), Does.StartWith("REF-"));
        Assert.That(doc.RootElement.GetProperty("number").GetString(), Does.Not.StartWith("RCPT-"));

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent($$"""{"checkout_id":"{{checkoutId}}"}""", Encoding.UTF8, "application/json")
        };
        replay.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        replay.Headers.TryAddWithoutValidation("Idempotency-Key", "ref-1");
        var second = await client.SendAsync(replay);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK), await second.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Charges.Single().Status, Is.EqualTo("refunded"));
        Assert.That(db.JournalLines.Count(x => x.Account == "cash" && x.Dc == "C"), Is.EqualTo(1));
        Assert.That(db.Documents.Count(x => x.Title == "Refund"), Is.EqualTo(1));
        Assert.That(db.OrgWebhookDeliveries.Count(x => x.EventType == "refund.created"), Is.EqualTo(1));
    }

    [Test]
    public async Task Partial_then_remainder()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var partial = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent($$"""{"checkout_id":"{{checkoutId}}","amount":4}""", Encoding.UTF8, "application/json")
        };
        partial.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var first = await client.SendAsync(partial);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created), await first.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Charges.Single().Status, Is.EqualTo("partially_refunded"));

        using var rest = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent($$"""{"checkout_id":"{{checkoutId}}"}""", Encoding.UTF8, "application/json")
        };
        rest.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var second = await client.SendAsync(rest);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.Created), await second.Content.ReadAsStringAsync());
        using var scope2 = factory.Services.CreateScope();
        Assert.That(scope2.ServiceProvider.GetRequiredService<PayDbContext>().Charges.Single().Status, Is.EqualTo("refunded"));
    }

    [Test]
    public async Task Solana_refund_is_refused_and_releases_the_reservation()
    {
        // Pins the refusal to the provider label, not just the shared throw exercised via Billplz.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Add(new CheckoutRow
            {
                Id = "c_sol", OrgId = "t1", PublicToken = "tok_sol",
                Amount = 10m, Currency = "USDC", Status = "paid", Provider = "solana"
            });
            db.Charges.Add(new ChargeRow
            {
                Id = "ch_sol", OrgId = "t1", CheckoutId = "c_sol",
                Provider = "solana", Amount = 10m, Currency = "USDC", Status = "paid"
            });
            await db.SaveChangesAsync();
        }

        using var refund = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent("""{"checkout_id":"c_sol"}""", Encoding.UTF8, "application/json")
        };
        refund.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(refund);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("refund not supported"));

        using var after = factory.Services.CreateScope();
        var db2 = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db2.Refunds.Single().Status, Is.EqualTo("failed"));
        Assert.That(db2.Charges.Single().Status, Is.EqualTo("paid"));
    }

    [Test]
    public async Task Stripe_refund_without_session_or_intent_fails_closed()
    {
        // A refund whose capture cannot be located at the processor must not read as settled.
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);

        var payload = "{\"id\":\"evt_fs\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_fs_1\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = $"t={t},v1={Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(factory.StripeWebhookSecret), Encoding.UTF8.GetBytes($"{t}.{payload}"))).ToLowerInvariant()}";
        using var pay = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        pay.Headers.TryAddWithoutValidation("Stripe-Signature", sig);
        Assert.That((await client.SendAsync(pay)).IsSuccessStatusCode, Is.True);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var checkout = db.Checkouts.Single();
            checkout.ProviderSessionId = null;
            var charge = db.Charges.Single();
            charge.ProviderRef = null;
            await db.SaveChangesAsync();
        }

        using var refund = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/refunds")
        {
            Content = new StringContent($$"""{"checkout_id":"{{checkoutId}}"}""", Encoding.UTF8, "application/json")
        };
        refund.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(refund);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), await response.Content.ReadAsStringAsync());

        using var after = factory.Services.CreateScope();
        var db2 = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db2.Refunds.Single().Status, Is.EqualTo("failed"));
        Assert.That(db2.Charges.Single().Status, Is.EqualTo("paid"));
    }

    [Test]
    public async Task Paid_webhook_on_expired_does_not_fulfill()
    {
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

        var body = $$"""{"id":"evt_late","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("refunded"));
        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Documents.Count(), Is.EqualTo(0));
        Assert.That(pay.Charges.Count(), Is.EqualTo(0));
        Assert.That(pay.Checkouts.Single().Status, Is.EqualTo("expired"));
        Assert.That(pay.Refunds.Single().Reason, Is.EqualTo("late_pay"));
        Assert.That(pay.Refunds.Single().Status, Is.EqualTo("succeeded"));
    }
}
