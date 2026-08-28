using System.Net;
using System.Text;
using System.Text.Json;

namespace Lazuar.Pay.Tests;

public class MachineKeyTests
{
    static HttpRequestMessage BearerGet(string url, string token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        return req;
    }

    static HttpRequestMessage BearerPost(string url, string token, string json)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + token);
        return req;
    }

    [Test]
    public async Task Whoami_forwards_machine_key_shape()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        using var request = BearerGet("/v1/whoami", PayTest.MachineKey);
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("user_id").GetString(), Is.EqualTo("key-1"));
        Assert.That(doc.RootElement.GetProperty("tenants")[0].GetProperty("id").GetString(), Is.EqualTo("t1"));
        Assert.That(doc.RootElement.GetProperty("is_platform_admin").GetBoolean(), Is.False);
        Assert.That(factory.One.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/me"));
        Assert.That(factory.One.LastRequest?.Headers.Authorization?.ToString(), Is.EqualTo("Bearer " + PayTest.MachineKey));
    }

    [Test]
    public async Task Key_ready_does_not_call_authz_check()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        using var request = BearerGet("/v1/orgs/t1/ready", PayTest.MachineKey);
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        Assert.That(factory.One.LastRequest?.Method, Is.EqualTo(HttpMethod.Get));
        Assert.That(factory.One.LastRequest?.RequestUri?.AbsolutePath, Does.EndWith("/me"));
    }

    [Test]
    public async Task Key_member_role_can_create_checkout()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        using var request = BearerPost(
            "/v1/checkouts",
            PayTest.MachineKey,
            """{"org_id":"t1","amount":10,"provider":"test"}""");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Jwt_member_still_cannot_create_checkout()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = req =>
        {
            var path = req.RequestUri?.AbsolutePath ?? "";
            if (req.Method == HttpMethod.Get && path.EndsWith("/me"))
            {
                return FakeOneHandler.Json(
                    HttpStatusCode.OK,
                    """{"user_id":"u1","is_platform_admin":false,"tenants":[{"id":"t1","role":"member","status":"active"}]}""");
            }

            return FakeOneHandler.Json(HttpStatusCode.OK, """{"allowed":true}""");
        };
        var client = factory.CreateClient();
        using var request = BearerPost(
            "/v1/checkouts",
            "tok",
            """{"org_id":"t1","amount":10,"provider":"test"}""");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("Writer role required"));
    }

    [Test]
    public async Task Key_bound_to_other_tenant_is_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.KeyFor("t1");
        var client = factory.CreateClient();
        using var mint = BearerPost(
            "/v1/checkouts",
            PayTest.MachineKey,
            """{"org_id":"t2","amount":10,"provider":"test"}""");
        var minted = await client.SendAsync(mint);
        Assert.That(minted.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));

        using var ready = BearerGet("/v1/orgs/t2/ready", PayTest.MachineKey);
        var listed = await client.SendAsync(ready);
        Assert.That(listed.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await listed.Content.ReadAsStringAsync(), Does.Not.Contain("user_id is required"));
    }

    [Test]
    public async Task Key_suspended_tenant_is_403()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.KeyFor("t1", "suspended");
        var client = factory.CreateClient();
        using var request = BearerGet("/v1/orgs/t1/ready", PayTest.MachineKey);
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("suspend"));
    }

    [Test]
    public async Task Revoked_key_is_401()
    {
        await using var factory = new PayApiFactory();
        var n = 0;
        factory.One.Responder = _ =>
        {
            n++;
            return n == 1
                ? FakeOneHandler.Json(HttpStatusCode.OK, PayTest.KeyMeJson())
                : new HttpResponseMessage(HttpStatusCode.Unauthorized);
        };
        var client = factory.CreateClient();
        using var first = BearerGet("/v1/whoami", PayTest.MachineKey);
        Assert.That((await client.SendAsync(first)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var second = BearerGet("/v1/whoami", PayTest.MachineKey);
        Assert.That((await client.SendAsync(second)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        using var mint = BearerPost(
            "/v1/checkouts",
            PayTest.MachineKey,
            """{"org_id":"t1","amount":10,"provider":"test"}""");
        Assert.That((await client.SendAsync(mint)).StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Missing_bearer_does_not_use_env_key()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        var response = await client.GetAsync("/v1/orgs/t1/ready");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
        Assert.That(factory.One.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Scope_403_is_not_not_a_member()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = _ => FakeOneHandler.Json(
            HttpStatusCode.Forbidden,
            """{"detail":"API key lacks required scope authz:check."}""");
        var client = factory.CreateClient();
        using var request = BearerGet("/v1/orgs/t1/ready", "tok");
        var response = await client.SendAsync(request);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("scope"));
        Assert.That(body, Does.Not.Contain("Not a member"));
    }
}
