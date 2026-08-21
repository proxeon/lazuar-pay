using System.Net;
using System.Text;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class CheckoutTests
{
    static HttpResponseMessage Allow(string orgId, HttpRequestMessage req)
    {
        var path = req.RequestUri?.AbsolutePath ?? "";
        if (req.Method == HttpMethod.Post && path.Contains($"/tenants/{orgId}/authz/check"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        }

        if (req.Method == HttpMethod.Post && path.Contains("/authz/check"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":false}""");
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    static HttpRequestMessage JsonPost(string url, string json)
    {
        return new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    [Test]
    public async Task Create_without_bearer_is_401()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        var response = await client.SendAsync(JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10}"""));
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Create_and_get_open_session()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":12.50,"currency":"myr","success_url":"https://ok.test","cancel_url":"https://no.test"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetString();
        Assert.That(doc.RootElement.GetProperty("org_id").GetString(), Is.EqualTo("t1"));
        Assert.That(doc.RootElement.GetProperty("amount").GetDecimal(), Is.EqualTo(12.50m));
        Assert.That(doc.RootElement.GetProperty("currency").GetString(), Is.EqualTo("MYR"));
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement.GetProperty("success_url").GetString(), Is.EqualTo("https://ok.test"));
        Assert.That(doc.RootElement.GetProperty("cancel_url").GetString(), Is.EqualTo("https://no.test"));

        using var get = new HttpRequestMessage(HttpMethod.Get, $"/v1/checkouts/{id}");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var fetched = await client.SendAsync(get);
        Assert.That(fetched.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var got = JsonDocument.Parse(await fetched.Content.ReadAsStringAsync());
        Assert.That(got.RootElement.GetProperty("id").GetString(), Is.EqualTo(id));
    }

    [Test]
    public async Task Get_unknown_is_404()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/checkouts/missing");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(get);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Create_for_other_org_is_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t2","amount":10}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Get_other_org_session_is_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetString();

        factory.One.Responder = req => Allow("t2", req);
        using var get = new HttpRequestMessage(HttpMethod.Get, $"/v1/checkouts/{id}");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var fetched = await client.SendAsync(get);
        Assert.That(fetched.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Create_idempotent_on_key()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();

        async Task<string> Post()
        {
            using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10}""");
            create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
            create.Headers.TryAddWithoutValidation("Idempotency-Key", "k1");
            var response = await client.SendAsync(create);
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            return doc.RootElement.GetProperty("id").GetString()!;
        }

        var a = await Post();
        var b = await Post();
        Assert.That(b, Is.EqualTo(a));
    }

    [Test]
    public async Task Create_defaults_currency_to_myr()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("currency").GetString(), Is.EqualTo("MYR"));
    }

    [Test]
    public async Task Create_rejects_non_positive_amount()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":0}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Health_still_skips_one()
    {
        await using var factory = new PayApiFactory();
        factory.One.ThrowOnSend = true;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/health");
        Assert.That(response.IsSuccessStatusCode);
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }
}
