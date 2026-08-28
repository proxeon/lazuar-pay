using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class GatewayTests
{
    static HttpResponseMessage Role(string role, HttpRequestMessage req)
    {
        var path = req.RequestUri?.AbsolutePath ?? "";
        if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, $$"""{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"{{role}}","status":"active"}]}""");
        }

        return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
    }

    [Test]
    public async Task Member_cannot_put_gateway()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("member", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_x"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(keys);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Put_requires_webhook_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(keys);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Put_and_get_does_not_echo_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var put = await client.SendAsync(keys);
        Assert.That(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());
        var putBody = await put.Content.ReadAsStringAsync();
        Assert.That(putBody, Does.Not.Contain("sk_test_dummy"));
        Assert.That(putBody, Does.Not.Contain("whsec_abc"));
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway?provider=stripe");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var got = await client.SendAsync(get);
        var json = await got.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("configured").GetBoolean());
        Assert.That(doc.RootElement.GetProperty("provider").GetString(), Is.EqualTo("stripe"));
        Assert.That(doc.RootElement.GetProperty("capability").GetString(), Is.EqualTo("hosted_link"));
        Assert.That(doc.RootElement.GetProperty("webhook_configured").GetBoolean());
        Assert.That(json, Does.Not.Contain("sk_test"));
        Assert.That(json, Does.Not.Contain("whsec_abc"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.AuditEvents.Any(a => a.Action == "gateway.credentials.upsert" && a.OrgId == "t1"));
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.Null);
    }

    [Test]
    public async Task Chip_put_requires_brand_id()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"chip","secret":"chip_sk","webhook_secret":"-----BEGIN PUBLIC KEY-----\nM\n-----END PUBLIC KEY-----"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(keys);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Put_unknown_provider_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
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
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).IsSuccessStatusCode);
        factory.One.Responder = req => Role("member", req);
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway?provider=stripe");
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
    public async Task List_returns_all_five_and_put_does_not_default_pay_links()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");
        await PayTest.PutChip(client);

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateways");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var got = await client.SendAsync(list);
        Assert.That(got.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var doc = JsonDocument.Parse(await got.Content.ReadAsStringAsync());
        var processors = doc.RootElement.GetProperty("processors");
        Assert.That(processors.GetArrayLength(), Is.EqualTo(6));
        var stripe = processors.EnumerateArray().Single(p => p.GetProperty("provider").GetString() == "stripe");
        var chip = processors.EnumerateArray().Single(p => p.GetProperty("provider").GetString() == "chip");
        var xendit = processors.EnumerateArray().Single(p => p.GetProperty("provider").GetString() == "xendit");
        var test = processors.EnumerateArray().Single(p => p.GetProperty("provider").GetString() == "test");
        Assert.That(stripe.GetProperty("configured").GetBoolean());
        Assert.That(chip.GetProperty("configured").GetBoolean());
        Assert.That(xendit.GetProperty("configured").GetBoolean(), Is.False);
        Assert.That(test.GetProperty("configured").GetBoolean());

        using var bare = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway");
        bare.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var bareGot = await client.SendAsync(bare);
        using var bareDoc = JsonDocument.Parse(await bareGot.Content.ReadAsStringAsync());
        Assert.That(bareDoc.RootElement.GetProperty("processors").GetArrayLength(), Is.EqualTo(6));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.Null);
        Assert.That(db.GatewayCredentials.Count(), Is.EqualTo(2));
    }

    [Test]
    public async Task Put_test_processor_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"test","secret":"x","webhook_secret":"y"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(keys);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("does not take secrets"));
    }

    [Test]
    public async Task Get_unknown_provider_query_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway?provider=paypal");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(get)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Billplz_put_requires_collection_id()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
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
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"razorpay","secret":"nocolon","webhook_secret":"wh"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(keys)).StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Chip_put_rejects_non_pem_webhook_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var keys = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"chip","secret":"chip_sk","webhook_secret":"nope","public_merchant_id":"brand_1"}""", Encoding.UTF8, "application/json")
        };
        keys.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(keys);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("PEM"));
    }

    [Test]
    public async Task Stripe_put_without_environment_keeps_previous()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Role("owner", req);
        var client = factory.CreateClient();
        using var first = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_live_dummy","webhook_secret":"whsec_abc","environment":"live"}""", Encoding.UTF8, "application/json")
        };
        first.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        Assert.That((await client.SendAsync(first)).IsSuccessStatusCode);
        using var second = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent("""{"provider":"stripe","secret":"sk_live_dummy2","webhook_secret":"whsec_abc"}""", Encoding.UTF8, "application/json")
        };
        second.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var put = await client.SendAsync(second);
        Assert.That(put.IsSuccessStatusCode, await put.Content.ReadAsStringAsync());
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway?provider=stripe");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        using var doc = JsonDocument.Parse(await (await client.SendAsync(get)).Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("environment").GetString(), Is.EqualTo("live"));
    }
}
