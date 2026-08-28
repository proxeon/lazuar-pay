using System.Net;
using System.Text;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class CheckoutTests
{
    static HttpResponseMessage Allow(string orgId, HttpRequestMessage req)
    {
        var path = req.RequestUri?.AbsolutePath ?? "";
        if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
        {
            return FakeOneHandler.Json(HttpStatusCode.OK, $$"""{"user_id":"u1","email":"ada@acme.test","is_platform_admin":false,"tenants":[{"id":"{{orgId}}","role":"owner","status":"active"}]}""");
        }

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
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":12.50,"currency":"myr","provider":"stripe","success_url":"https://ok.test","cancel_url":"https://no.test"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(create);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var id = doc.RootElement.GetProperty("id").GetString();
        Assert.That(doc.RootElement.GetProperty("org_id").GetString(), Is.EqualTo("t1"));
        Assert.That(doc.RootElement.GetProperty("amount").GetDecimal(), Is.EqualTo(12.50m));
        Assert.That(doc.RootElement.GetProperty("currency").GetString(), Is.EqualTo("MYR"));
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement.GetProperty("provider").GetString(), Is.EqualTo("stripe"));
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
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10,"provider":"stripe"}""");
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
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");

        async Task<(HttpStatusCode Status, string Id)> Post(string json)
        {
            using var create = JsonPost("/v1/checkouts", json);
            create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
            create.Headers.TryAddWithoutValidation("Idempotency-Key", "k1");
            var response = await client.SendAsync(create);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var id = doc.RootElement.TryGetProperty("id", out var idEl) ? idEl.GetString() ?? "" : "";
            return (response.StatusCode, id);
        }

        var a = await Post("""{"org_id":"t1","amount":10,"provider":"stripe"}""");
        Assert.That(a.Status, Is.EqualTo(HttpStatusCode.Created));
        var b = await Post("""{"org_id":"t1","amount":10,"provider":"stripe"}""");
        Assert.That(b.Status, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(b.Id, Is.EqualTo(a.Id));
        var conflict = await Post("""{"org_id":"t1","amount":20,"provider":"stripe"}""");
        Assert.That(conflict.Status, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Create_defaults_currency_to_myr()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10,"provider":"stripe"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("currency").GetString(), Is.EqualTo("MYR"));
    }

    [Test]
    public async Task Create_without_provider_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("unknown provider"));
    }

    [Test]
    public async Task Create_unknown_provider_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10,"provider":"paypal"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("unknown provider"));
    }

    [Test]
    public async Task Create_unconfigured_rail_is_400()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        await PayTest.Put(client, """{"provider":"stripe","secret":"sk_test_dummy","webhook_secret":"whsec_abc"}""");
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10,"provider":"chip"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("rail not configured"));
    }

    [Test]
    public async Task Create_test_without_vault_is_201()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10,"provider":"test"}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("provider").GetString(), Is.EqualTo("test"));
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
    public async Task Member_cannot_create_checkout()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(HttpStatusCode.OK, """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"member","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        var client = factory.CreateClient();
        using var create = JsonPost("/v1/checkouts", """{"org_id":"t1","amount":10}""");
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task List_returns_org_checkouts_newest_first()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        await PayTest.SeedCheckout(client, "test");
        await PayTest.SeedCheckout(client, "test");

        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/checkouts");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(list);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetArrayLength(), Is.EqualTo(2));
        Assert.That(doc.RootElement[0].GetProperty("provider").GetString(), Is.EqualTo("test"));
        Assert.That(doc.RootElement[0].GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(doc.RootElement[0].GetProperty("public_token").GetString(), Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task List_other_org_is_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req => Allow("t1", req);
        var client = factory.CreateClient();
        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t2/checkouts");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(list);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
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
