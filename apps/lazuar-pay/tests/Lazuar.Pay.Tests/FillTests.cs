using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Gateways;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class FillTests
{
    static HttpResponseMessage Owner(HttpRequestMessage req)
    {
        var path = req.RequestUri?.AbsolutePath ?? "";
        if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"owner","status":"active"}]}""");
        }

        return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
    }

    static async Task Put(HttpClient client, string json)
    {
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(keys);
        Assert.That(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
    }

    static async Task<(string Token, string CheckoutId)> SeedCheckout(HttpClient client)
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("public_token").GetString()!, doc.RootElement.GetProperty("id").GetString()!);
    }

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
        factory.One.Responder = Owner;
        factory.Probe.ThrowNext = true;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).IsSuccessStatusCode);
        var (_, checkoutId) = await SeedCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single(x => x.Id == checkoutId).Provider = "stripe";
            await db.SaveChangesAsync();
        }

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
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        await client.SendAsync(keys);
        var (_, checkoutId) = await SeedCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single().Provider = "stripe";
            await db.SaveChangesAsync();
        }

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
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        await client.SendAsync(keys);
        var (_, checkoutId) = await SeedCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<PayDbContext>().Checkouts.Single().Provider = "stripe";
            await scope.ServiceProvider.GetRequiredService<PayDbContext>().SaveChangesAsync();
        }

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
    public async Task Missing_stripe_signature_header_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        await client.SendAsync(keys);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/stripe/t1")
        {
            Content = new StringContent("""{"id":"evt_x","type":"checkout.session.completed"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
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
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_test_local"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        await client.SendAsync(keys);
        var (_, checkoutId) = await SeedCheckout(client);
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
    public async Task Chip_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(factory.Psp.LastUri, Is.Null);
    }

    [Test]
    public async Task Billplz_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"x","public_merchant_id":"col","environment":"test"}""");
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Xendit_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"xendit","secret":"xnd","webhook_secret":"tok"}""");
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Razorpay_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}""");
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Billplz_localhost_callback_start_is_400_without_psp_http()
    {
        await using var factory = new PayApiFactory { PublicBaseUrl = "http://localhost:8081" };
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"x","public_merchant_id":"col","environment":"test"}""");
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("callback base not public"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Billplz_unpaid_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"xsig","public_merchant_id":"col","environment":"test"}""");
        var (token, checkoutId) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).IsSuccessStatusCode);
        var form = "id=bill_u&paid=false&state=due&paid_amount=0&currency=MYR&x_signature=pending&checkout_id=" + checkoutId;
        var fields = BillplzWebhook.ParseForm(form);
        var mac = BillplzWebhook.ComputeHmac(fields, "xsig", excludeExtra: false);
        form = "id=bill_u&paid=false&state=due&paid_amount=0&currency=MYR&x_signature=" + mac + "&checkout_id=" + checkoutId;
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1?checkout_id=" + checkoutId)
        {
            Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("unpaid"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Billplz_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"billplz","secret":"bp","webhook_secret":"x","public_merchant_id":"col","environment":"test"}""");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Xendit_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"xendit","secret":"xnd","webhook_secret":"tok"}""");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/xendit/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Razorpay_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}""");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Razorpay_payment_failed_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, checkoutId) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = "{\"event\":\"payment.failed\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"amount\":1000,\"currency\":\"MYR\",\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}}}}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("payment_failed"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Razorpay_captured_without_notes_joins_plink()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = """{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":1000,"currency":"MYR"}},"payment_link":{"entity":{"id":"plink_1"}}}}""";
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

    [Test]
    public async Task Put_unknown_provider_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"paypal","secret":"x","webhook_secret":"y"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Member_can_get_gateway_metadata()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"member","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var got = await client.SendAsync(get);
        Assert.That(got.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var json = await got.Content.ReadAsStringAsync();
        Assert.That(json, Does.Not.Contain("sk_test"));
        Assert.That(json, Does.Not.Contain("whsec"));
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("provider").GetString(), Is.EqualTo("stripe"));
        Assert.That(doc.RootElement.GetProperty("capability").GetString(), Is.EqualTo("hosted_link"));
    }

    [Test]
    public async Task Email_required_true_when_active_chip()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        var (token, _) = await SeedCheckout(client);
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("email_required").GetBoolean());
    }

    [Test]
    public async Task Email_required_false_when_active_stripe()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}""");
        var (token, _) = await SeedCheckout(client);
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("email_required").GetBoolean(), Is.False);
    }

    [Test]
    public async Task Start_without_rail_is_503()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("rail not configured"));
    }

    [Test]
    public async Task Billplz_put_requires_collection_id()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"billplz","secret":"bp","webhook_secret":"x","environment":"test"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Razorpay_put_requires_key_id_colon_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"razorpay","secret":"nocolon","webhook_secret":"wh"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
