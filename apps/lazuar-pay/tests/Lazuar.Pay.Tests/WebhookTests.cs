using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class WebhookTests
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

    static string Sign(string secret, string payload, long t)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{t}.{payload}"));
        return $"t={t},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    static async Task<string> SeedRailAndCheckout(HttpClient client)
    {
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).IsSuccessStatusCode, Is.True);

        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetString()!;
    }

    [Test]
    public async Task Missing_webhook_secret_is_503_when_rail_configured()
    {
        await using var factory = new PayApiFactory { StripeWebhookSecret = "" };
        factory.One.Responder = Owner;
        var client = factory.CreateClient();
        await SeedRailAndCheckout(client);
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
        factory.One.Responder = Owner;
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
        factory.One.Responder = Owner;
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
}
