using System.Net;
using System.Text;
using Lazuar.Pay.Data;
using Lazuar.Pay.Identity;
using Lazuar.Pay.Identity.Client;
using Microsoft.Extensions.Caching.Memory;
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

/// <summary>
/// Issue 010 (issues/003): the reverse indexes must retire entries with their tokens
/// (direct membership index, no full-scan eviction) and stay consistent when a token's
/// cache generation is replaced or re-removed — a stale eviction callback must never prune
/// a newer generation's index.
/// </summary>
public class WhoamiCacheIndexTests
{
    static WhoamiResponse Who(string orgId) => new()
    {
        UserId = "key-1",
        Tenants = [new WhoamiTenant { Id = orgId, Role = "owner", Status = "active" }]
    };

    [Test]
    public void Remove_token_prunes_only_its_own_memberships()
    {
        var cache = new OneWhoamiCache(new MemoryCache(new MemoryCacheOptions()));
        cache.Set("tok_a", Who("t1"), machineKey: true);
        cache.Set("tok_b", Who("t1"), machineKey: true);
        cache.Set("tok_c", Who("t2"), machineKey: true);
        Assert.That(cache.TrackedIndexEntries, Is.EqualTo(3));

        cache.RemoveToken("tok_a");
        Assert.That(cache.TrackedIndexEntries, Is.EqualTo(2));
        Assert.That(cache.TryGet("tok_a", out _), Is.False);
        Assert.That(cache.TryGet("tok_b", out _), Is.True);

        cache.InvalidateOrg("t1");
        Assert.That(cache.TryGet("tok_b", out _), Is.False);
        Assert.That(cache.TryGet("tok_c", out _), Is.True, "another org's token must survive");

        cache.InvalidateKey("key-1");
        Assert.That(cache.TryGet("tok_c", out _), Is.False);
        Assert.That(cache.TrackedIndexEntries, Is.EqualTo(0), "the direct index must retire with the tokens");
    }

    [Test]
    public void Replacing_and_reremoving_a_token_generation_keeps_the_index_consistent()
    {
        var cache = new OneWhoamiCache(new MemoryCache(new MemoryCacheOptions()));
        cache.Set("tok_x", Who("t9"), machineKey: true);

        // Replace the cache generation for the same token: the first generation's eviction
        // callback may fire at any time; the instance check must keep the live index.
        cache.Set("tok_x", Who("t9"), machineKey: true);
        Assert.That(cache.TryGet("tok_x", out _), Is.True);

        // Remove twice — the second (and any stale in-flight callback) must be harmless.
        cache.RemoveToken("tok_x");
        cache.RemoveToken("tok_x");
        Assert.That(cache.TryGet("tok_x", out _), Is.False);
        Assert.That(cache.TrackedIndexEntries, Is.EqualTo(0));
    }
}

