using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class ChipRailTests
{
    [Test]
    public async Task Chip_start_and_paid_webhook()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = pem, public_merchant_id = "brand_1" }));
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "chip");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        var startedBody = await started.Content.ReadAsStringAsync();
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK), startedBody);
        using var startDoc = JsonDocument.Parse(startedBody);
        Assert.That(startDoc.RootElement.GetProperty("redirect_url").GetString(), Is.EqualTo("https://gate.chip-in.asia/p/x"));
        Assert.That(factory.Psp.LastBody, Does.Not.Contain("force_recurring"));
        Assert.That(factory.Psp.LastBody, Does.Contain("checkout_id"));
        Assert.That(factory.Psp.LastBody, Does.Contain("org_id"));

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
        Assert.That(db.Checkouts.Single().Provider, Is.EqualTo("chip"));
        Assert.That(db.Checkouts.Single().ProviderSessionId, Is.EqualTo("purch_1"));
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
    public async Task Chip_paid_without_metadata_joins_on_purchase_id()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = pem, public_merchant_id = "brand_1" }));
        var (token, _) = await PayTest.SeedCheckout(client, "chip");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var payload = """{"event_type":"purchase.paid","id":"purch_1","purchase":{"id":"purch_1","total":1000,"currency":"MYR"}}""";
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
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
    }

    [Test]
    public async Task Chip_preauthorized_is_ignored()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        using var rsa = RSA.Create(2048);
        var pem = rsa.ExportSubjectPublicKeyInfoPem();
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = pem, public_merchant_id = "brand_1" }));
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "chip");
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
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        var (token, _) = await PayTest.SeedCheckout(client, "chip");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Chip_empty_body_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/chip/t1")
        {
            Content = new StringContent("  ", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Chip_placeholder_email_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        var (token, _) = await PayTest.SeedCheckout(client, "chip");
        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"customer@example.com"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(factory.Psp.LastUri, Is.Null);
    }
}
