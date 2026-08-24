using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Gateways;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class RailTests
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

    static async Task<(string Token, string CheckoutId)> SeedCheckout(HttpClient client)
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return (doc.RootElement.GetProperty("public_token").GetString()!, doc.RootElement.GetProperty("id").GetString()!);
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

    [Test]
    public async Task Chip_start_and_paid_webhook()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = pem, public_merchant_id = "brand_1" }));
        var (token, checkoutId) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK), await started.Content.ReadAsStringAsync());
        Assert.That(factory.Psp.LastBody, Does.Not.Contain("force_recurring"));
        Assert.That(factory.Psp.LastBody, Does.Contain("checkout_id"));

        var payload = "{\"event_type\":\"purchase.paid\",\"id\":\"purch_1\",\"purchase\":{\"id\":\"purch_1\",\"total\":1000,\"currency\":\"MYR\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\",\"org_id\":\"t1\"}}}";
        var sig = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/chip/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Signature", sig);
        var paid = await client.SendAsync(wh);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
        Assert.That(db.JournalLines.Where(l => l.Dc == "D").Sum(l => l.Amount), Is.EqualTo(db.JournalLines.Where(l => l.Dc == "C").Sum(l => l.Amount)));

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/chip/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        replay.Headers.TryAddWithoutValidation("X-Signature", sig);
        var second = await client.SendAsync(replay);
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Chip_preauthorized_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        var client = factory.CreateClient();
        await Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = pem, public_merchant_id = "brand_1" }));
        var (_, checkoutId) = await SeedCheckout(client);
        var payload = "{\"event_type\":\"purchase.preauthorized\",\"id\":\"purch_1\",\"purchase\":{\"id\":\"purch_1\",\"total\":0,\"currency\":\"MYR\",\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}},\"recurring_token\":\"tok\"}";
        var sig = Convert.ToBase64String(rsa.SignData(Encoding.UTF8.GetBytes(payload), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/chip/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Signature", sig);
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("preauthorized"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Chip_start_without_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        var (token, _) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Billplz_paid_form_and_localhost_blocked()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"bill_1","url":"https://www.billplz-sandbox.com/bills/bill_1"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await Put(client, """{"provider":"billplz","secret":"bp_sk","webhook_secret":"xsig","public_merchant_id":"col_1","environment":"test"}""");
        var (token, checkoutId) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());
        Assert.That(factory.Psp.LastUri!.ToString(), Does.Contain("billplz-sandbox"));

        var form = "id=bill_1&paid=true&state=paid&paid_amount=1000&x_signature=pending&checkout_id=" + checkoutId;
        var fields = BillplzWebhook.ParseForm(form);
        fields["x_signature"] = "pending";
        var mac = BillplzWebhook.ComputeHmac(fields, "xsig", excludeExtra: false);
        form = "id=bill_1&paid=true&state=paid&paid_amount=1000&x_signature=" + mac + "&checkout_id=" + checkoutId;
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/billplz/t1?checkout_id=" + checkoutId)
        {
            Content = new StringContent(form, Encoding.UTF8, "application/x-www-form-urlencoded")
        };
        var paid = await client.SendAsync(wh);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Xendit_paid_and_settled()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"inv_1","invoice_url":"https://checkout.xendit.co/inv_1"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await Put(client, """{"provider":"xendit","secret":"xnd_sk","webhook_secret":"tok_1"}""");
        var (token, checkoutId) = await SeedCheckout(client);
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());

        var payload = "{\"id\":\"inv_1\",\"status\":\"PAID\",\"currency\":\"MYR\",\"paid_amount\":10,\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}";
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/xendit/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("x-callback-token", "tok_1");
        var paid = await client.SendAsync(wh);
        Assert.That(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync());

        var settled = "{\"id\":\"inv_1\",\"status\":\"SETTLED\",\"currency\":\"MYR\",\"paid_amount\":10,\"metadata\":{\"checkout_id\":\"" + checkoutId + "\"}}";
        using var wh2 = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/xendit/t1")
        {
            Content = new StringContent(settled, Encoding.UTF8, "application/json")
        };
        wh2.Headers.TryAddWithoutValidation("x-callback-token", "tok_1");
        var second = await client.SendAsync(wh2);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Razorpay_captured()
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
        var started = await client.SendAsync(start);
        Assert.That(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());

        var payload = "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"amount\":1000,\"currency\":\"MYR\",\"tax\":12,\"fee\":30,\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}}}}";
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
        Assert.That(db.JournalLines.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Chip_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/chip/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }
}
