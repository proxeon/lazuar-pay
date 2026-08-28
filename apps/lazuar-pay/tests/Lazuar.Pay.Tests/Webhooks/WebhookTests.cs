using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class WebhookTests
{
    static string Sign(string secret, string payload, long t)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{t}.{payload}"));
        return $"t={t},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    static async Task<string> SeedRailAndCheckout(HttpClient client)
    {
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""");
        var (_, checkoutId) = await PayTest.SeedCheckout(client);
        return checkoutId;
    }

    [Test]
    public async Task Missing_webhook_secret_is_503_when_rail_configured()
    {
        await using var factory = new PayApiFactory { StripeWebhookSecret = "" };
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await SeedRailAndCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            var row = db.GatewayCredentials.Single();
            row.WebhookCiphertext = null;
            await db.SaveChangesAsync();
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent("""{"id":"evt_x"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Invalid_signature_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await SeedRailAndCheckout(client);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent("""{"id":"evt_x","type":"checkout.session.completed"}""", Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", "t=1,v1=deadbeef");
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Completed_session_writes_receipt_and_replay_is_noop()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var checkoutId = await SeedRailAndCheckout(client);
        var eventId = "evt_test_" + Guid.NewGuid().ToString("N");
        var payload =
            "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_test_1\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\",\"org_id\":\"t1\"}}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var first = await client.SendAsync(req);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), await first.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
        Assert.That(db.Documents.Single().Title, Is.EqualTo("Official Receipt"));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
        Assert.That(db.OrgSettings.Single().SstRegistered, Is.Null);
        Assert.That(await first.Content.ReadAsStringAsync(), Does.Not.Contain("SST registration unknown"));
        var debit = db.JournalLines.Where(l => l.Dc == "D").Sum(l => l.Amount);
        var credit = db.JournalLines.Where(l => l.Dc == "C").Sum(l => l.Amount);
        Assert.That(debit, Is.EqualTo(credit));

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        replay.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var second = await client.SendAsync(replay);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Setup_mode_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var checkoutId = await SeedRailAndCheckout(client);
        var eventId = "evt_setup_" + Guid.NewGuid().ToString("N");
        var payload =
            "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_setup\",\"object\":\"checkout.session\",\"mode\":\"setup\",\"amount_total\":0,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"unpaid\",\"status\":\"complete\"}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ignored"));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("setup"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(0));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
    }

    [Test]
    public async Task Zero_amount_session_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var checkoutId = await SeedRailAndCheckout(client);
        var eventId = "evt_zero_" + Guid.NewGuid().ToString("N");
        var payload =
            "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_zero\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":0,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\"}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ignored"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(0));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
    }

    [Test]
    public async Task Cross_org_checkout_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var checkoutId = await SeedRailAndCheckout(client);
        using var keys2 = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t2/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""", Encoding.UTF8, "application/json")
        };
        keys2.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t2","role":"owner","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        Assert.That((await client.SendAsync(keys2)).IsSuccessStatusCode);
        factory.One.Responder = PayTest.Owner;
        var eventId = "evt_xorg_" + Guid.NewGuid().ToString("N");
        var payload =
            "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_x\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\"}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t2")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Unknown_provider_is_400()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/paypal/t1")
        {
            Content = new StringContent("""{"id":"x"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Paused_org_does_not_mint_receipt()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var checkoutId = await SeedRailAndCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused = true;
            await db.SaveChangesAsync();
        }

        var eventId = "evt_paused_" + Guid.NewGuid().ToString("N");
        var payload =
            "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_paused\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\",\"org_id\":\"t1\"}}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict), await response.Content.ReadAsStringAsync());
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.Documents.Count(), Is.EqualTo(0));
            Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
            Assert.That(db.PspWebhookEvents.Count(e => e.EventId == eventId), Is.EqualTo(0));
        }

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused = false;
            await db.SaveChangesAsync();
        }

        using var retry = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        retry.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var paid = await client.SendAsync(retry);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        using var after = factory.Services.CreateScope();
        Assert.That(after.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Unpaid_completed_session_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var checkoutId = await SeedRailAndCheckout(client);
        var eventId = "evt_unpaid_" + Guid.NewGuid().ToString("N");
        var payload =
            "{\"id\":\"" + eventId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_unpaid\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"unpaid\",\"status\":\"complete\"}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, payload, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("ignored"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(0));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
    }

    [Test]
    public async Task Async_payment_succeeded_pays_after_unpaid_completed()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var checkoutId = await SeedRailAndCheckout(client);
        var unpaidId = "evt_unpaid2_" + Guid.NewGuid().ToString("N");
        var unpaid =
            "{\"id\":\"" + unpaidId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.completed\",\"data\":{\"object\":{\"id\":\"cs_async\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"unpaid\",\"status\":\"complete\"}}}";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using (var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(unpaid, Encoding.UTF8, "application/json")
        })
        {
            req.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, unpaid, t));
            Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        }

        var paidId = "evt_async_" + Guid.NewGuid().ToString("N");
        var paid =
            "{\"id\":\"" + paidId + "\",\"object\":\"event\",\"api_version\":\"2024-06-20\",\"created\":1700000000,\"livemode\":false,\"pending_webhooks\":1,\"request\":{\"id\":null},\"type\":\"checkout.session.async_payment_succeeded\",\"data\":{\"object\":{\"id\":\"cs_async\",\"object\":\"checkout.session\",\"mode\":\"payment\",\"amount_total\":1000,\"currency\":\"myr\",\"client_reference_id\":\"" + checkoutId + "\",\"payment_status\":\"paid\",\"status\":\"complete\"}}}";
        using var paidReq = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent(paid, Encoding.UTF8, "application/json")
        };
        paidReq.Headers.TryAddWithoutValidation("Stripe-Signature", Sign(factory.StripeWebhookSecret, paid, t));
        var response = await client.SendAsync(paidReq);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
    }
}
