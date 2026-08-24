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
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway");
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
        Assert.That(db.OrgSettings.Single().ActiveProvider, Is.EqualTo("stripe"));
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
}
