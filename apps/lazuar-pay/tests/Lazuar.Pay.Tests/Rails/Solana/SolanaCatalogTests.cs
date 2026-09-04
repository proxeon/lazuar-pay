using System.Net;
using System.Text;
using Lazuar.Pay.Rails;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Lazuar.Pay.Tests;

public class SolanaCatalogTests
{
    [Test]
    public void PayProviders_knows_solana()
    {
        Assert.That(PayProviders.Solana, Is.EqualTo("solana"));
        Assert.That(PayProviders.All, Does.Contain(PayProviders.Solana));
        Assert.That(PayProviders.All, Does.Not.Contain(PayProviders.Test));
        Assert.That(PayProviders.Capability, Is.EqualTo("hosted_link"));
        Assert.That(PayProviders.TryNormalize("solana", out var a), Is.True);
        Assert.That(a, Is.EqualTo("solana"));
        Assert.That(PayProviders.TryNormalize(" Solana ", out var b), Is.True);
        Assert.That(b, Is.EqualTo("solana"));
        Assert.That(PayProviders.TryNormalize("paypal", out _), Is.False);
        Assert.That(PayProviders.RequiresEmail(PayProviders.Solana), Is.False);
        Assert.That(PayProviders.RequiresPublicMerchantId(PayProviders.Solana), Is.True);
        Assert.That(PayProviders.AllowsPublicMerchantId(PayProviders.Solana), Is.True);
        Assert.That(PayProviders.AllowsPublicMerchantId(PayProviders.Stripe), Is.False);

        var testing = new NamedEnv("Testing");
        var production = new NamedEnv("Production");
        Assert.That(PayProviders.Listed(testing), Does.Contain(PayProviders.Solana));
        Assert.That(PayProviders.Listed(testing), Does.Contain(PayProviders.Test));
        Assert.That(PayProviders.Listed(testing).Count, Is.EqualTo(7));
        Assert.That(PayProviders.Listed(production), Does.Contain(PayProviders.Solana));
        Assert.That(PayProviders.Listed(production), Does.Not.Contain(PayProviders.Test));
        Assert.That(PayProviders.Listed(production).Count, Is.EqualTo(6));
    }

    [Test]
    public async Task Paypal_is_still_unknown_provider()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"paypal"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("unknown provider"));
    }

    [Test]
    public async Task Solana_mint_without_vault_is_rail_not_configured()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent("""{"org_id":"t1","amount":10,"provider":"solana","currency":"USDC"}""", Encoding.UTF8, "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var response = await client.SendAsync(create);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await response.Content.ReadAsStringAsync(), Does.Contain("rail not configured"));
    }

    [Test]
    public async Task Solana_plane_b_webhook_is_not_stripe_parse()
    {
        await using var factory = new PayApiFactory();
        var client = factory.CreateClient();
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/solana/t1")
        {
            Content = new StringContent("""{"type":"checkout.session.completed"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(req);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(body, Does.Contain("solana does not use inbound PSP webhooks").Or.Contain("invalid signature"));
    }

    sealed class NamedEnv(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "test";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
