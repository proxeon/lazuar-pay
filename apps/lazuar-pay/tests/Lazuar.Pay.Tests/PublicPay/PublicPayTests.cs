using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class PublicPayTests
{
    [Test]
    public async Task Public_get_does_not_need_bearer()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}""");
        var (token, _) = await PayTest.SeedCheckout(client);
        var get = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(get.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(factory.One.SendCount, Is.GreaterThan(0));
        var after = factory.One.SendCount;
        var again = await client.GetAsync($"/v1/pay/{token}");
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(factory.One.SendCount, Is.EqualTo(after));
    }

    [Test]
    public async Task Public_missing_is_404()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/pay/missing");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Start_twice_returns_same_url_without_second_psp_http()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent(
                """{"provider":"chip","secret":"chip_sk","webhook_secret":"-----BEGIN PUBLIC KEY-----\nMFwwDQYJKoZIhvcNAQEBBQADSwAwSAJBAL0=\n-----END PUBLIC KEY-----","public_merchant_id":"brand_1"}""",
                Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).IsSuccessStatusCode, Is.True);

        var (token, checkoutId) = await PayTest.SeedCheckout(client, "chip");

        using var start1 = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var first = await client.SendAsync(start1);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), await first.Content.ReadAsStringAsync());
        using var firstDoc = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        var url = firstDoc.RootElement.GetProperty("redirect_url").GetString();
        Assert.That(url, Is.EqualTo("https://gate.chip-in.asia/p/x"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));

        using var start2 = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var second = await client.SendAsync(start2);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK), await second.Content.ReadAsStringAsync());
        using var secondDoc = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.That(secondDoc.RootElement.GetProperty("redirect_url").GetString(), Is.EqualTo(url));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(1));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var row = db.Checkouts.Single(x => x.Id == checkoutId);
        Assert.That(row.ProviderSessionId, Is.EqualTo("purch_1"));
        Assert.That(row.Provider, Is.EqualTo("chip"));
    }

    [Test]
    public async Task Public_get_exposes_started_and_redirect_after_start()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"id":"purch_1","checkout_url":"https://gate.chip-in.asia/p/x"}""", Encoding.UTF8, "application/json")
        };
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent(
                """{"provider":"chip","secret":"chip_sk","webhook_secret":"k","public_merchant_id":"brand_1"}""",
                Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).IsSuccessStatusCode, Is.True);

        var (token, _) = await PayTest.SeedCheckout(client, "chip");

        var before = await client.GetAsync($"/v1/pay/{token}");
        using var beforeDoc = JsonDocument.Parse(await before.Content.ReadAsStringAsync());
        Assert.That(beforeDoc.RootElement.GetProperty("started").GetBoolean(), Is.False);
        Assert.That(beforeDoc.RootElement.TryGetProperty("redirect_url", out var beforeUrl) && beforeUrl.ValueKind != JsonValueKind.Null && beforeUrl.ValueKind != JsonValueKind.Undefined, Is.False);

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada","email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        Assert.That((await client.SendAsync(start)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var after = await client.GetAsync($"/v1/pay/{token}");
        using var afterDoc = JsonDocument.Parse(await after.Content.ReadAsStringAsync());
        Assert.That(afterDoc.RootElement.GetProperty("started").GetBoolean(), Is.True);
        Assert.That(afterDoc.RootElement.GetProperty("redirect_url").GetString(), Is.EqualTo("https://gate.chip-in.asia/p/x"));
    }

    [Test]
    public async Task Start_paid_is_409()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single(x => x.Id == checkoutId).Status = "paid";
            db.Checkouts.Single(x => x.Id == checkoutId).PspRedirectUrl = "https://already.example/x";
            await db.SaveChangesAsync();
        }

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Start_paused_is_403_even_with_stored_url()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}""");
        var (token, checkoutId) = await PayTest.SeedCheckout(client);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single(x => x.Id == checkoutId).PspRedirectUrl = "https://gate.chip-in.asia/p/x";
            var settings = db.OrgSettings.Single(x => x.OrgId == "t1");
            settings.ChargesPaused = true;
            await db.SaveChangesAsync();
        }

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Email_required_true_when_active_chip()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new { provider = "chip", secret = "chip_sk", webhook_secret = "pem", public_merchant_id = "brand_1" }));
        var (token, _) = await PayTest.SeedCheckout(client, "chip");
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("email_required").GetBoolean());
    }

    [Test]
    public async Task Email_required_false_when_active_stripe()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}""");
        var (token, _) = await PayTest.SeedCheckout(client);
        var get = await client.GetAsync($"/v1/pay/{token}");
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("email_required").GetBoolean(), Is.False);
    }

    [Test]
    public async Task Start_without_rail_is_503()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        const string token = "legacyopen";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Add(new CheckoutRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = "t1",
                PublicToken = token,
                Amount = 10m,
                Currency = "MYR",
                Status = "open",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("rail not configured"));
    }

    [Test]
    public async Task Start_does_not_read_leftover_ActiveProvider()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec"}""");
        const string token = "legacyactive";
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.OrgSettings.Single(x => x.OrgId == "t1").ActiveProvider = "stripe";
            db.Checkouts.Add(new CheckoutRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = "t1",
                PublicToken = token,
                Amount = 10m,
                Currency = "MYR",
                Status = "open",
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        using var start = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"email":"ada@acme.test"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(start);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("rail not configured"));
    }
}
