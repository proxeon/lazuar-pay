using System.Net;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Rails.Solana;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class SolanaHostedTests
{
    [Test]
    public async Task Start_returns_solana_pay_url_and_stays_open()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var address = SolanaVaultTests.SampleAddress();
        await PayTest.Put(client, JsonSerializer.Serialize(new
        {
            provider = "solana",
            public_merchant_id = address,
            environment = "devnet"
        }));
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "solana", "USDC");
        var started = await PayTest.StartPay(client, token, slotKey: null, json: """{"name":"Ada"}""");
        var body = await started.Content.ReadAsStringAsync();
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);
        using var startDoc = JsonDocument.Parse(body);
        Assert.That(startDoc.RootElement.TryGetProperty("redirect_url", out _), Is.False);
        var url = startDoc.RootElement.GetProperty("solana_pay_url").GetString();
        Assert.That(url, Does.StartWith("solana:" + address));
        Assert.That(url, Does.Contain("spl-token=" + SolanaUsdc.DevnetMint));
        Assert.That(url, Does.Contain("amount=10"));
        Assert.That(url, Does.Contain("reference="));
        Assert.That(url, Does.Contain("memo=" + checkoutId));

        using var get = await client.GetAsync("/v1/pay/" + token);
        using var getDoc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(getDoc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(getDoc.RootElement.GetProperty("started").GetBoolean());
        Assert.That(getDoc.RootElement.GetProperty("email_required").GetBoolean(), Is.False);
        Assert.That(getDoc.RootElement.GetProperty("redirect_url").ValueKind, Is.EqualTo(JsonValueKind.Null));
        Assert.That(getDoc.RootElement.GetProperty("solana_pay_url").GetString(), Is.EqualTo(url));
        Assert.That(getDoc.RootElement.GetProperty("solana_cluster").GetString(), Is.EqualTo("devnet"));
        Assert.That(getDoc.RootElement.TryGetProperty("pay_url", out _), Is.False);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var row = db.Checkouts.Single();
        Assert.That(row.Status, Is.EqualTo("open"));
        Assert.That(row.PspRedirectUrl, Is.Null);
        Assert.That(row.SolanaPayUrl, Is.EqualTo(url));
        Assert.That(row.ProviderSessionId, Is.Not.Empty);
        Assert.That(db.Documents.Count(), Is.EqualTo(0));

        var again = await PayTest.StartPay(client, token, slotKey: null, json: """{"name":"Ada"}""");
        using var againDoc = JsonDocument.Parse(await again.Content.ReadAsStringAsync());
        Assert.That(againDoc.RootElement.GetProperty("solana_pay_url").GetString(), Is.EqualTo(url));
        Assert.That(db.Checkouts.Single().ProviderSessionId, Is.EqualTo(row.ProviderSessionId));
    }

    [Test]
    public async Task Slot_key_resumes_the_same_qr_without_a_second_seat()
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
        var (token, _) = await PayTest.SeedPaymentLink(client, "solana", maxPayers: 1, currency: "USDC");
        var first = await PayTest.StartPay(client, token, slotKey: "slot-key-1", json: """{"name":"Ada"}""");
        var firstBody = await first.Content.ReadAsStringAsync();
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.OK), firstBody);
        using var firstDoc = JsonDocument.Parse(firstBody);
        var url = firstDoc.RootElement.GetProperty("solana_pay_url").GetString();

        var second = await PayTest.StartPay(client, token, slotKey: "slot-key-1", json: """{"name":"Ada"}""");
        using var secondDoc = JsonDocument.Parse(await second.Content.ReadAsStringAsync());
        Assert.That(secondDoc.RootElement.GetProperty("solana_pay_url").GetString(), Is.EqualTo(url));

        using var get = await client.GetAsync("/v1/pay/" + token + "?slot_key=slot-key-1");
        using var getDoc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(getDoc.RootElement.GetProperty("solana_pay_url").GetString(), Is.EqualTo(url));
        Assert.That(getDoc.RootElement.GetProperty("taken_count").GetInt32(), Is.EqualTo(1));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Count(), Is.EqualTo(1));
    }
}
