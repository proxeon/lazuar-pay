using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Rails;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Tests;

public class TestRailTests
{
    [Test]
    public async Task Mint_and_start_pays_without_keys()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "test");

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        var started = await client.SendAsync(start);
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK), await started.Content.ReadAsStringAsync());
        using var startDoc = JsonDocument.Parse(await started.Content.ReadAsStringAsync());
        Assert.That(startDoc.RootElement.GetProperty("redirect_url").GetString(), Does.Contain("status=verifying"));

        var get = await client.GetAsync($"/v1/pay/{token}");
        using var pay = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(pay.RootElement.GetProperty("status").GetString(), Is.EqualTo("paid"));
        Assert.That(pay.RootElement.GetProperty("provider").GetString(), Is.EqualTo("test"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Single(x => x.Id == checkoutId).Status, Is.EqualTo("paid"));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
        Assert.That(db.Documents.Single().Title, Is.EqualTo("Official Receipt"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Webhook_pays_open_test_checkout()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        var body = $$"""{"id":"evt_test_1","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""";
        using var req = SignedTestWebhook(body);
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Unsigned_test_webhook_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(
                $$"""{"id":"evt_unsigned","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""",
                Encoding.UTF8,
                "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public void AllowsTest_is_laptop_and_hermetic_only()
    {
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Development")), Is.True);
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Testing")), Is.True);
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Staging")), Is.False);
        Assert.That(PayProviders.AllowsTest(new NamedEnv("Production")), Is.False);
    }

    [Test]
    public async Task Test_webhook_without_amount_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var req = SignedTestWebhook($$"""{"id":"evt_omit","checkout_id":"{{checkoutId}}","currency":"myr"}""");
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Test_webhook_wrong_amount_does_not_consume_event()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var req = SignedTestWebhook($$"""{"id":"evt_mm","checkout_id":"{{checkoutId}}","amount_total":10,"currency":"myr"}""");
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Documents.Count(), Is.EqualTo(0));
        Assert.That(db.PspWebhookEvents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Test_webhook_without_id_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        using var req = SignedTestWebhook($$"""{"checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""");
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("event id"));
    }

    [Test]
    public async Task Test_webhook_replay_same_id_is_duplicate()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var (_, checkoutId) = await PayTest.SeedCheckout(client, "test");
        var body = $$"""{"id":"evt_dup","checkout_id":"{{checkoutId}}","amount_total":1000,"currency":"myr"}""";
        Assert.That((await client.SendAsync(SignedTestWebhook(body))).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var replay = SignedTestWebhook(body);
        var second = await client.SendAsync(replay);
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().Documents.Count(), Is.EqualTo(1));
    }

    static HttpRequestMessage SignedTestWebhook(string body)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        return req;
    }

    sealed class NamedEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "Lazuar.Pay.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
