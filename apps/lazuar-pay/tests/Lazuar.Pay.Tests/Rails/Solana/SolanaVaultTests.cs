using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Rails.Solana;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class SolanaVaultTests
{
    public static string SampleAddress()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return SolanaBase58.Encode(bytes);
    }

    [Test]
    public async Task Put_solana_address_without_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var address = SampleAddress();
        await PayTest.Put(client, JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = address,
            environment = "devnet"
        }));

        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway?provider=solana");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var got = await client.SendAsync(get);
        var json = await got.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        Assert.That(doc.RootElement.GetProperty("configured").GetBoolean());
        Assert.That(doc.RootElement.GetProperty("public_merchant_id").GetString(), Is.EqualTo(address));
        Assert.That(doc.RootElement.GetProperty("environment").GetString(), Is.EqualTo("devnet"));
        Assert.That(doc.RootElement.GetProperty("last4").GetString(), Is.EqualTo(address[^4..]));
        Assert.That(doc.RootElement.GetProperty("webhook_configured").GetBoolean(), Is.False);
        Assert.That(doc.RootElement.GetProperty("capability").GetString(), Is.EqualTo("hosted_link"));
        Assert.That(json, Does.Not.Contain("sk_"));
        Assert.That(json, Does.Not.Contain("whsec_"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var row = db.GatewayCredentials.Single(x => x.Provider == "solana");
        Assert.That(row.Ciphertext, Is.EqualTo(""));
        Assert.That(row.WebhookCiphertext, Is.Null);
    }

    [Test]
    public async Task Put_solana_rejects_secret_and_webhook_secret()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var address = SampleAddress();
        using var secret = JsonPut(JsonSerializer.Serialize(new
        {
            provider = "solana",
            secret = "sk_test_x",
            public_merchant_id = address,
            environment = "devnet"
        }));
        var secretRes = await client.SendAsync(secret);
        Assert.That(secretRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await secretRes.Content.ReadAsStringAsync(), Does.Contain("API secret"));

        using var wh = JsonPut(JsonSerializer.Serialize(new
        {
            provider = "solana",
            webhook_secret = "whsec_x",
            public_merchant_id = address,
            environment = "devnet"
        }));
        var whRes = await client.SendAsync(wh);
        Assert.That(whRes.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await whRes.Content.ReadAsStringAsync(), Does.Contain("webhook secret"));
    }

    [Test]
    public async Task Put_solana_rejects_invalid_address_and_rpc()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        foreach (var bad in new[]
                 {
                     """{"provider":"solana","public_merchant_id":"not-an-address","environment":"devnet"}""",
                     """{"provider":"solana","public_merchant_id":"0xabc","environment":"devnet"}""",
                     """{"provider":"solana","public_merchant_id":"https://api.devnet.solana.com","environment":"devnet"}""",
                     """{"provider":"solana","public_merchant_id":"-----BEGIN","environment":"devnet"}""",
                     """{"provider":"solana","environment":"devnet"}"""
                 })
        {
            using var req = JsonPut(bad);
            var res = await client.SendAsync(req);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), bad);
        }
    }

    [Test]
    public async Task Put_solana_requires_devnet_or_mainnet()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var address = SampleAddress();
        using var live = JsonPut(JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = address,
            environment = "live"
        }));
        var res = await client.SendAsync(live);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await res.Content.ReadAsStringAsync(), Does.Contain("devnet or mainnet"));

        await PayTest.Put(client, JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = address,
            environment = "mainnet-beta"
        }));
        using var get = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/gateway?provider=solana");
        get.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        using var doc = JsonDocument.Parse(await (await client.SendAsync(get)).Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("environment").GetString(), Is.EqualTo("mainnet"));
    }

    [Test]
    public async Task Member_cannot_put_solana()
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
        using var req = JsonPut(JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = SampleAddress(),
            environment = "devnet"
        }));
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Stripe_still_rejects_public_merchant_id()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var req = JsonPut("""{"provider":"stripe","secret":"sk_test_x","webhook_secret":"whsec_x","public_merchant_id":"brand"}""");
        var res = await client.SendAsync(req);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await res.Content.ReadAsStringAsync(), Does.Contain("not used for this provider"));
    }

    static HttpRequestMessage JsonPut(string json)
    {
        var req = new HttpRequestMessage(HttpMethod.Put, "/v1/orgs/t1/gateway")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        return req;
    }
}
