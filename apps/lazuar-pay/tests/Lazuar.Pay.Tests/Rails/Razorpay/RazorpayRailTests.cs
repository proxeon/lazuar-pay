using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class RazorpayRailTests
{
    [Test]
    public async Task Razorpay_captured()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.IsSuccessStatusCode, await started.Content.ReadAsStringAsync());

        var payload = "{\"event\":\"payment.captured\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"amount\":1000,\"currency\":\"INR\",\"tax\":12,\"fee\":30,\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}}}}";
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
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
        Assert.That(db.JournalLines.Count(), Is.EqualTo(2));
        Assert.That(db.JournalLines.Where(l => l.Dc == "D").Sum(l => l.Amount), Is.EqualTo(db.JournalLines.Where(l => l.Dc == "C").Sum(l => l.Amount)));
    }

    [Test]
    public async Task Razorpay_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}""");
        var (token, _) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Razorpay_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh"}""");
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
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = "{\"event\":\"payment.failed\",\"payload\":{\"payment\":{\"entity\":{\"id\":\"pay_1\",\"amount\":1000,\"currency\":\"INR\",\"notes\":{\"checkout_id\":\"" + checkoutId + "\"}}}}}";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("wh_rzp"), Encoding.UTF8.GetBytes(payload));
        using var wh = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/razorpay/t1")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        wh.Headers.TryAddWithoutValidation("X-Razorpay-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        var response = await client.SendAsync(wh);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("failed"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Razorpay_captured_without_notes_joins_plink()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"plink_1","short_url":"https://rzp.io/i/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"razorpay","secret":"rzp_test:secret","webhook_secret":"wh_rzp"}""");
        var (token, _) = await PayTest.SeedCheckout(client, "razorpay", "INR");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        await client.SendAsync(start);
        var payload = """{"event":"payment.captured","payload":{"payment":{"entity":{"id":"pay_1","amount":1000,"currency":"INR"}},"payment_link":{"entity":{"id":"plink_1"}}}}""";
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
}
