using System.Net;
using System.Security.Cryptography;
using System.Text;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class OneWebhookTests
{
    const string Secret = "one_whsec_test";

    static string Sign(string body, long unix)
    {
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{unix}.{body}"));
        return $"t={unix},v1={Convert.ToHexString(mac).ToLowerInvariant()}";
    }

    [Test]
    public async Task Valid_tenant_suspended_sets_charges_paused()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"id":"del_1","type":"tenant.suspended","org_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused, Is.True);
    }

    [Test]
    public async Task Valid_tenant_id_field_sets_charges_paused()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"id":"del_tenant","type":"tenant.suspended","tenant_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused, Is.True);
    }

    [Test]
    public async Task Body_only_uppercase_hex_is_401()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"id":"del_old","type":"tenant.suspended","org_id":"t1"}""";
        var hex = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes(body)));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", hex);
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Missing_signature_is_401()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent("""{"id":"del_x","type":"tenant.suspended","org_id":"t1"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Stale_timestamp_is_401()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"id":"del_stale","type":"tenant.suspended","org_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 1000;
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Missing_secret_is_503()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = "" };
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent("""{"id":"x"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
    }

    [Test]
    public async Task Replay_delivery_is_duplicate()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"id":"del_replay","type":"tenant.suspended","org_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var sig = Sign(body, t);
        using var firstReq = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        firstReq.Headers.TryAddWithoutValidation("X-Lazuar-Signature", sig);
        Assert.That((await client.SendAsync(firstReq)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var secondReq = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        secondReq.Headers.TryAddWithoutValidation("X-Lazuar-Signature", sig);
        var second = await client.SendAsync(secondReq);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
    }

    [Test]
    public async Task Tenant_reactivated_clears_pause()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var suspend = """{"id":"del_s","type":"tenant.suspended","org_id":"t1"}""";
        using var sReq = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(suspend, Encoding.UTF8, "application/json")
        };
        sReq.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(suspend, t));
        Assert.That((await client.SendAsync(sReq)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var reactivate = """{"id":"del_r","type":"tenant.reactivated","org_id":"t1"}""";
        using var rReq = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(reactivate, Encoding.UTF8, "application/json")
        };
        rReq.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(reactivate, t));
        Assert.That((await client.SendAsync(rReq)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused, Is.False);
    }

    [Test]
    public async Task Product_one_split_headers_suspend_charges()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"id":"del_one","type":"tenant.suspended","tenant_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes(Secret), Encoding.UTF8.GetBytes($"{t}.{body}"));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", "v1=" + Convert.ToHexString(mac).ToLowerInvariant());
        req.Headers.TryAddWithoutValidation("X-Lazuar-Timestamp", t.ToString());
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<PayDbContext>().OrgSettings.Single(x => x.OrgId == "t1").ChargesPaused, Is.True);
    }

    [Test]
    public async Task Empty_signed_body_is_400()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = "";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Garbage_signed_body_is_400()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = "not-json";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("invalid event"));
    }

    [Test]
    public async Task Missing_body_id_uses_event_id_header()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"type":"tenant.suspended","org_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        req.Headers.TryAddWithoutValidation("X-Lazuar-Event-Id", "del_header");
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var replay = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        replay.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        replay.Headers.TryAddWithoutValidation("X-Lazuar-Event-Id", "del_header");
        var second = await client.SendAsync(replay);
        Assert.That(second.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(await second.Content.ReadAsStringAsync(), Does.Contain("duplicate"));
    }

    [Test]
    public async Task Signed_json_without_event_id_is_400()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = Secret };
        var client = factory.CreateClient();
        var body = """{"type":"tenant.suspended","org_id":"t1"}""";
        var t = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Lazuar-Signature", Sign(body, t));
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("event id required"));
    }
}
