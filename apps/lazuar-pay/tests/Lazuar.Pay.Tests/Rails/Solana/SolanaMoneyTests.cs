using System.Net;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Rails.Solana;

namespace Lazuar.Pay.Tests;

public class SolanaMoneyTests
{
    [Test]
    public void ToAtomic_is_six_decimals_not_cents()
    {
        Assert.That(SolanaMoney.TryToAtomic(10m, out var atomic), Is.True);
        Assert.That(atomic, Is.EqualTo(10_000_000L));
        Assert.That(SolanaMoney.TryToAtomic(10.1234567m, out _), Is.False);
        Assert.That(SolanaUsdc.MintFor("mainnet"), Is.EqualTo(SolanaUsdc.MainnetMint));
        Assert.That(SolanaUsdc.MintFor("devnet"), Is.EqualTo(SolanaUsdc.DevnetMint));
        Assert.That(SolanaUsdc.MainnetMint, Is.Not.EqualTo(SolanaUsdc.DevnetMint));
    }

    [Test]
    public async Task Solana_mint_requires_usdc()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = SolanaVaultTests.SampleAddress(),
            environment = "devnet"
        }));

        var ok = await CreateCheckout(client, """{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC"}""");
        Assert.That(ok.StatusCode, Is.EqualTo(HttpStatusCode.Created), await ok.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await ok.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("currency").GetString(), Is.EqualTo("USDC"));
        Assert.That(doc.RootElement.GetProperty("amount").GetDecimal(), Is.EqualTo(10m));

        foreach (var body in new[]
                 {
                     """{"org_id":"t1","amount":10,"provider":"solana"}""",
                     """{"org_id":"t1","amount":10,"provider":"solana","currency":"MYR"}""",
                     """{"org_id":"t1","amount":10,"provider":"solana","currency":"USD"}""",
                     """{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC","interval":"mo"}"""
                 })
        {
            var res = await CreateCheckout(client, body);
            Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest), body);
        }

        var linkMyr = await CreateLink(client, """{"org_id":"t1","amount":10,"provider":"solana","currency":"MYR"}""");
        Assert.That(linkMyr.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await linkMyr.Content.ReadAsStringAsync(), Does.Contain("ringgit"));

        var linkOk = await CreateLink(client, """{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC"}""");
        Assert.That(linkOk.StatusCode, Is.EqualTo(HttpStatusCode.Created), await linkOk.Content.ReadAsStringAsync());

        var tooManyDecimals = await CreateCheckout(client, """{"org_id":"t1","amount":10.1234567,"provider":"solana","currency":"USDC"}""");
        Assert.That(tooManyDecimals.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await tooManyDecimals.Content.ReadAsStringAsync(), Does.Contain("USDC amount"));
    }

    [Test]
    public async Task Solana_rejects_myr_catalog_product()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        await PayTest.Put(client, JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = SolanaVaultTests.SampleAddress(),
            environment = "devnet"
        }));
        using var product = new HttpRequestMessage(HttpMethod.Post, "/v1/orgs/t1/products")
        {
            Content = new StringContent("""{"name":"Bar","amount":10,"currency":"MYR"}""", Encoding.UTF8, "application/json")
        };
        product.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var created = await client.SendAsync(product);
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created), await created.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
        var productId = doc.RootElement.GetProperty("id").GetString();
        var link = await CreateLink(client, $$"""{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC","product_id":"{{productId}}"}""");
        Assert.That(link.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await link.Content.ReadAsStringAsync(), Does.Contain("catalog"));
    }

    static async Task<HttpResponseMessage> CreateCheckout(HttpClient client, string json)
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        return await client.SendAsync(create);
    }

    static async Task<HttpResponseMessage> CreateLink(HttpClient client, string json)
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/payment-links")
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        return await client.SendAsync(create);
    }
}
