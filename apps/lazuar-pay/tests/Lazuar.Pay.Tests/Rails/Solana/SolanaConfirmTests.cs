using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Lazuar.Pay.Rails;
using Lazuar.Pay.Rails.Solana;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

public class SolanaConfirmTests
{
    [Test]
    public async Task Confirm_paid_replay_and_mismatch()
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
        var started = await PayTest.StartPay(client, token, null, """{"name":"Ada"}""");
        Assert.That(started.StatusCode, Is.EqualTo(HttpStatusCode.OK), await started.Content.ReadAsStringAsync());
        using var startDoc = JsonDocument.Parse(await started.Content.ReadAsStringAsync());
        var url = startDoc.RootElement.GetProperty("solana_pay_url").GetString()!;
        var reference = ReferenceFrom(url);
        var signature = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));
        factory.Psp.Responder = (_, body) => RpcTx(body, Fixture(
            signature, address, SolanaUsdc.DevnetMint, "10000000", reference, checkoutId));

        using var confirm = Confirm(token, signature);
        var paid = await client.SendAsync(confirm);
        Assert.That(paid.StatusCode, Is.EqualTo(HttpStatusCode.OK), await paid.Content.ReadAsStringAsync());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
        Assert.That(db.Charges.Single().ProviderRef, Is.EqualTo(signature));
        Assert.That(db.Charges.Single().Currency, Is.EqualTo("USDC"));
        Assert.That(db.Documents.Single().Number, Does.StartWith("RCPT-"));
        Assert.That(db.PspWebhookEvents.Single().EventId, Is.EqualTo(signature));
        var envelope = db.OrgWebhookDeliveries.Select(x => x.PayloadJson).ToList();
        Assert.That(db.JournalLines.Where(l => l.Dc == "D").Sum(l => l.Amount), Is.EqualTo(10m));

        using var replay = Confirm(token, signature);
        Assert.That(await (await client.SendAsync(replay)).Content.ReadAsStringAsync(), Does.Contain("duplicate"));
        Assert.That(db.Documents.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task Confirm_mismatch_consumes_zero_events()
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
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var startDoc = JsonDocument.Parse(await (await client.GetAsync("/v1/pay/" + token)).Content.ReadAsStringAsync());
        var url = startDoc.RootElement.GetProperty("solana_pay_url").GetString()!;
        var reference = ReferenceFrom(url);
        var signature = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));
        factory.Psp.Responder = (_, body) => RpcTx(body, Fixture(
            signature, address, SolanaUsdc.DevnetMint, "1000", reference, checkoutId));

        using var confirm = Confirm(token, signature);
        var res = await client.SendAsync(confirm);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await res.Content.ReadAsStringAsync(), Does.Contain("amount mismatch"));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.PspWebhookEvents.Count(), Is.EqualTo(0));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
        Assert.That(db.Documents.Count(), Is.EqualTo(0));
    }

    [Test]
    public async Task Get_does_not_fulfill()
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
        var (token, _) = await PayTest.SeedCheckout(client, "solana", "USDC");
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        factory.Psp.Responder = (_, _) => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"jsonrpc":"2.0","result":null}""", Encoding.UTF8, "application/json")
        };
        using var get = await client.GetAsync("/v1/pay/" + token);
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("open"));
        Assert.That(factory.Psp.SendCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Pause_does_not_consume_signature()
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
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        db.OrgSettings.Single().ChargesPaused = true;
        await db.SaveChangesAsync();
        var signature = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));
        using var confirm = Confirm(token, signature);
        var res = await client.SendAsync(confirm);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        Assert.That(db.PspWebhookEvents.Count(), Is.EqualTo(0));
        _ = checkoutId;
    }

    [Test]
    public async Task Confirm_rejects_other_mint()
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
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var startDoc = JsonDocument.Parse(await (await client.GetAsync("/v1/pay/" + token)).Content.ReadAsStringAsync());
        var reference = ReferenceFrom(startDoc.RootElement.GetProperty("solana_pay_url").GetString()!);
        var signature = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));
        factory.Psp.Responder = (_, body) => RpcTx(body, Fixture(
            signature, address, SolanaUsdc.MainnetMint, "10000000", reference, checkoutId));
        using var confirm = Confirm(token, signature);
        var res = await client.SendAsync(confirm);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await res.Content.ReadAsStringAsync(), Does.Contain("mint mismatch"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.PspWebhookEvents.Count(), Is.EqualTo(0));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
    }

    [Test]
    public async Task Confirm_decoy_self_transfer_is_destination_mismatch()
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
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var startDoc = JsonDocument.Parse(await (await client.GetAsync("/v1/pay/" + token)).Content.ReadAsStringAsync());
        var reference = ReferenceFrom(startDoc.RootElement.GetProperty("solana_pay_url").GetString()!);
        var signature = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));
        factory.Psp.Responder = (_, body) => RpcTx(body, DecoyFixture(
            signature, address, SolanaUsdc.DevnetMint, "10000000", reference, checkoutId));
        using var confirm = Confirm(token, signature);
        var res = await client.SendAsync(confirm);
        Assert.That(res.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(await res.Content.ReadAsStringAsync(), Does.Contain("destination mismatch"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(db.PspWebhookEvents.Count(), Is.EqualTo(0));
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("open"));
    }

    [Test]
    public async Task Poller_walks_past_junk_signature()
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
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var startDoc = JsonDocument.Parse(await (await client.GetAsync("/v1/pay/" + token)).Content.ReadAsStringAsync());
        var url = startDoc.RootElement.GetProperty("solana_pay_url").GetString()!;
        var reference = ReferenceFrom(url);
        var junk = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));
        var good = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));
        factory.Psp.Responder = (_, body) =>
        {
            if (body is not null && body.Contains("getSignaturesForAddress", StringComparison.Ordinal))
            {
                return JsonRpc($$"""{"jsonrpc":"2.0","result":[{"signature":"{{junk}}"},{"signature":"{{good}}"}]}""");
            }

            if (body is not null && body.Contains(junk, StringComparison.Ordinal))
            {
                return JsonRpc(Fixture(junk, address, SolanaUsdc.DevnetMint, "1", reference, checkoutId));
            }

            return JsonRpc(Fixture(good, address, SolanaUsdc.DevnetMint, "10000000", reference, checkoutId));
        };

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var confirm = scope.ServiceProvider.GetRequiredService<SolanaConfirm>();
        await confirm.ConfirmOpenByReferenceAsync(CancellationToken.None);
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("paid"));
        Assert.That(db.Charges.Single().ProviderRef, Is.EqualTo(good));
    }

    [Test]
    public async Task Poller_watches_beyond_first_twenty()
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
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using var startDoc = JsonDocument.Parse(await (await client.GetAsync("/v1/pay/" + token)).Content.ReadAsStringAsync());
        var url = startDoc.RootElement.GetProperty("solana_pay_url").GetString()!;
        var reference = ReferenceFrom(url);
        var signature = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(64));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var newest = db.Checkouts.Single();
        newest.CreatedAt = DateTimeOffset.UtcNow;
        for (var i = 0; i < 20; i++)
        {
            db.Checkouts.Add(new CheckoutRow
            {
                Id = Guid.NewGuid().ToString("N"),
                OrgId = newest.OrgId,
                PublicToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
                Amount = newest.Amount,
                Currency = newest.Currency,
                Status = "open",
                Provider = PayProviders.Solana,
                PspRedirectUrl = newest.PspRedirectUrl,
                ProviderSessionId = SolanaBase58.Encode(RandomNumberGenerator.GetBytes(32)),
                CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-i - 1)
            });
        }

        await db.SaveChangesAsync();
        factory.Psp.Responder = (_, body) =>
        {
            if (body is not null && body.Contains("getSignaturesForAddress", StringComparison.Ordinal))
            {
                if (body.Contains(reference, StringComparison.Ordinal))
                {
                    return JsonRpc($$"""{"jsonrpc":"2.0","result":[{"signature":"{{signature}}"}]}""");
                }

                return JsonRpc("""{"jsonrpc":"2.0","result":[]}""");
            }

            return JsonRpc(Fixture(signature, address, SolanaUsdc.DevnetMint, "10000000", reference, checkoutId));
        };

        var confirm = scope.ServiceProvider.GetRequiredService<SolanaConfirm>();
        await confirm.ConfirmOpenByReferenceAsync(CancellationToken.None);
        Assert.That(db.Checkouts.Single(x => x.Id == checkoutId).Status, Is.EqualTo("paid"));
    }

    [Test]
    public async Task Start_failed_is_409()
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
        var (token, checkoutId) = await PayTest.SeedCheckout(client, "solana", "USDC");
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            db.Checkouts.Single(x => x.Id == checkoutId).Status = "failed";
            await db.SaveChangesAsync();
        }

        var again = await PayTest.StartPay(client, token, null);
        Assert.That(again.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        using var get = await client.GetAsync("/v1/pay/" + token);
        using var doc = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.That(doc.RootElement.GetProperty("status").GetString(), Is.EqualTo("failed"));
        Assert.That(doc.RootElement.GetProperty("solana_pay_url").ValueKind, Is.EqualTo(JsonValueKind.Null));
    }

    [Test]
    public async Task Stale_open_checkout_emits_payment_failed()
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
        var (token, _) = await PayTest.SeedCheckout(client, "solana", "USDC");
        Assert.That((await PayTest.StartPay(client, token, null)).StatusCode, Is.EqualTo(HttpStatusCode.OK));

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
        var row = db.Checkouts.Single();
        row.CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-31);
        await db.SaveChangesAsync();
        var confirm = scope.ServiceProvider.GetRequiredService<SolanaConfirm>();
        await confirm.ConfirmOpenByReferenceAsync(CancellationToken.None);
        Assert.That(db.Checkouts.Single().Status, Is.EqualTo("failed"));
        Assert.That(db.PspWebhookEvents.Single().EventId, Does.StartWith("watch_timeout:"));
        Assert.That(db.Documents.Count(), Is.EqualTo(0));
    }

    static HttpRequestMessage Confirm(string token, string signature)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/confirm")
        {
            Content = new StringContent(JsonSerializer.Serialize(new { signature }), Encoding.UTF8, "application/json")
        };
        return req;
    }

    static string ReferenceFrom(string url)
    {
        var q = url.Split('?', 2)[1];
        foreach (var part in q.Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv[0] == "reference")
            {
                return kv[1];
            }
        }

        throw new InvalidOperationException("reference missing");
    }

    static HttpResponseMessage RpcTx(string? requestBody, string resultJson)
    {
        if (requestBody is not null && requestBody.Contains("getSignaturesForAddress", StringComparison.Ordinal))
        {
            return JsonRpc("""{"jsonrpc":"2.0","result":[]}""");
        }

        return JsonRpc(resultJson);
    }

    static HttpResponseMessage JsonRpc(string json) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

    public const string MerchantAta = "Dest11111111111111111111111111111111111112";
    public const string BuyerAta = "Buyr11111111111111111111111111111111111112";

    public static string Fixture(string signature, string owner, string mint, string atomic, string reference, string memo) =>
        $$"""
        {
          "jsonrpc": "2.0",
          "result": {
            "slot": 1,
            "meta": {
              "err": null,
              "preTokenBalances": [
                { "accountIndex": 1, "mint": "{{mint}}", "owner": "{{owner}}", "uiTokenAmount": { "amount": "0", "decimals": 6 } }
              ],
              "postTokenBalances": [
                { "accountIndex": 1, "mint": "{{mint}}", "owner": "{{owner}}", "uiTokenAmount": { "amount": "{{atomic}}", "decimals": 6 } }
              ]
            },
            "transaction": {
              "signatures": ["{{signature}}"],
              "message": {
                "accountKeys": [
                  { "pubkey": "11111111111111111111111111111111", "signer": true, "writable": true },
                  { "pubkey": "{{MerchantAta}}", "signer": false, "writable": true },
                  { "pubkey": "{{SolanaUsdc.TokenProgram}}", "signer": false, "writable": false },
                  { "pubkey": "{{reference}}", "signer": false, "writable": false }
                ],
                "instructions": [
                  {
                    "programId": "{{SolanaUsdc.TokenProgram}}",
                    "parsed": {
                      "type": "transferChecked",
                      "info": {
                        "mint": "{{mint}}",
                        "destination": "{{MerchantAta}}",
                        "tokenAmount": { "amount": "{{atomic}}", "decimals": 6 }
                      }
                    }
                  },
                  {
                    "programId": "{{SolanaTx.MemoProgram}}",
                    "parsed": "{{memo}}"
                  }
                ]
              }
            }
          }
        }
        """;

    public static string DecoyFixture(string signature, string owner, string mint, string atomic, string reference, string memo) =>
        $$"""
        {
          "jsonrpc": "2.0",
          "result": {
            "slot": 1,
            "meta": {
              "err": null,
              "preTokenBalances": [
                { "accountIndex": 1, "mint": "{{mint}}", "owner": "buyer", "uiTokenAmount": { "amount": "0", "decimals": 6 } },
                { "accountIndex": 2, "mint": "{{mint}}", "owner": "{{owner}}", "uiTokenAmount": { "amount": "0", "decimals": 6 } }
              ],
              "postTokenBalances": [
                { "accountIndex": 1, "mint": "{{mint}}", "owner": "buyer", "uiTokenAmount": { "amount": "{{atomic}}", "decimals": 6 } },
                { "accountIndex": 2, "mint": "{{mint}}", "owner": "{{owner}}", "uiTokenAmount": { "amount": "0", "decimals": 6 } }
              ]
            },
            "transaction": {
              "signatures": ["{{signature}}"],
              "message": {
                "accountKeys": [
                  { "pubkey": "11111111111111111111111111111111", "signer": true, "writable": true },
                  { "pubkey": "{{BuyerAta}}", "signer": false, "writable": true },
                  { "pubkey": "{{MerchantAta}}", "signer": false, "writable": true },
                  { "pubkey": "{{SolanaUsdc.TokenProgram}}", "signer": false, "writable": false },
                  { "pubkey": "{{reference}}", "signer": false, "writable": false }
                ],
                "instructions": [
                  {
                    "programId": "{{SolanaUsdc.TokenProgram}}",
                    "parsed": {
                      "type": "transferChecked",
                      "info": {
                        "mint": "{{mint}}",
                        "destination": "{{BuyerAta}}",
                        "tokenAmount": { "amount": "{{atomic}}", "decimals": 6 }
                      }
                    }
                  },
                  {
                    "programId": "{{SolanaTx.MemoProgram}}",
                    "parsed": "{{memo}}"
                  }
                ]
              }
            }
          }
        }
        """;
}
