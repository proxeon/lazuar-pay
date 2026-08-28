using System.Net;
using System.Text;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class WhoamiCacheTests
{
    [Test]
    public async Task Revoke_event_then_mint_is_401()
    {
        await using var factory = new PayApiFactory { OneWebhookSecret = "one_whsec_cache" };
        factory.One.Responder = PayTest.Key;
        var client = factory.CreateClient();
        using var mint = BearerPost("/v1/checkouts", PayTest.MachineKey, """{"org_id":"t1","amount":10,"provider":"test"}""");
        var first = await client.SendAsync(mint);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created), await first.Content.ReadAsStringAsync());
        var before = factory.One.SendCount;

        using var mint2 = BearerPost("/v1/checkouts", PayTest.MachineKey, """{"org_id":"t1","amount":11,"provider":"test"}""");
        var cached = await client.SendAsync(mint2);
        Assert.That(cached.StatusCode, Is.EqualTo(HttpStatusCode.Created), await cached.Content.ReadAsStringAsync());
        Assert.That(factory.One.SendCount, Is.EqualTo(before));

        var body = """{"id":"del_rev","type":"api_key.revoked","data":{"key_id":"key-1","tenant_id":"t1"}}""";
        var unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var mac = System.Security.Cryptography.HMACSHA256.HashData(
            Encoding.UTF8.GetBytes("one_whsec_cache"),
            Encoding.UTF8.GetBytes($"{unix}.{body}"));
        using var rev = new HttpRequestMessage(HttpMethod.Post, "/v1/one/webhooks")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        rev.Headers.TryAddWithoutValidation("X-Lazuar-Signature", $"t={unix},v1={Convert.ToHexString(mac).ToLowerInvariant()}");
        Assert.That((await client.SendAsync(rev)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        factory.One.Responder = _ => FakeOneHandler.Json(HttpStatusCode.Unauthorized, """{"detail":"revoked"}""");
        using var mint3 = BearerPost("/v1/checkouts", PayTest.MachineKey, """{"org_id":"t1","amount":12,"provider":"test"}""");
        var after = await client.SendAsync(mint3);
        Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
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
}
