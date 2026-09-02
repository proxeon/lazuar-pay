using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Lazuar.Pay.Data;
using Microsoft.Extensions.DependencyInjection;

namespace Lazuar.Pay.Tests;

/// <summary>
/// Issue 004 (issues/001): past_due was a dead end — Start 409'd on every non-open checkout
/// and nothing ever wrote the subscription active again. The failed subscription checkout is
/// now re-openable so the payer can retry; failed one-off checkouts stay terminal.
/// </summary>
public class DunningRecoveryTests
{
    static async Task<string> CreateCheckout(HttpClient client, string intervalJson)
    {
        using var create = new HttpRequestMessage(HttpMethod.Post, "/v1/checkouts")
        {
            Content = new StringContent(
                $$"""{"org_id":"t1","amount":10,"provider":"test"{{intervalJson}}}""",
                Encoding.UTF8,
                "application/json")
        };
        create.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        var minted = await client.SendAsync(create);
        Assert.That(minted.StatusCode, Is.EqualTo(HttpStatusCode.Created), await minted.Content.ReadAsStringAsync());
        using var doc = JsonDocument.Parse(await minted.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("public_token").GetString()!;
    }

    static async Task Fail(HttpClient client, string token)
    {
        using var scope0 = new HttpRequestMessage(HttpMethod.Get, $"/v1/pay/{token}");
        // resolve checkout id via list is awkward; use the seeded org list instead
        _ = scope0;
        using var list = new HttpRequestMessage(HttpMethod.Get, "/v1/orgs/t1/checkouts?limit=1");
        list.Headers.TryAddWithoutValidation("Authorization", "Bearer tok");
        using var listDoc = JsonDocument.Parse(await (await client.SendAsync(list)).Content.ReadAsStringAsync());
        var checkoutId = PayTest.Items(listDoc.RootElement)[0].GetProperty("id").GetString()!;

        var body = $$"""{"id":"evt_fail_{{Guid.NewGuid():N}}","checkout_id":"{{checkoutId}}","status":"failed","currency":"myr"}""";
        var mac = HMACSHA256.HashData(Encoding.UTF8.GetBytes("test_whsec_local"), Encoding.UTF8.GetBytes(body));
        using var req = new HttpRequestMessage(HttpMethod.Post, "/v1/webhooks/test/t1")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.TryAddWithoutValidation("X-Pay-Test-Signature", Convert.ToHexString(mac).ToLowerInvariant());
        Assert.That((await client.SendAsync(req)).StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Past_due_subscription_checkout_can_be_retried_back_to_active()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var token = await CreateCheckout(client, ",\"interval\":\"mo\"");
        await Fail(client, token);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PayDbContext>();
            Assert.That(db.Subscriptions.Single().Status, Is.EqualTo("past_due"));
            Assert.That(db.Checkouts.Single().Status, Is.EqualTo("failed"));
        }

        // The retry: start on the same token now succeeds instead of 409. The test rail
        // fulfills immediately, flipping the subscription back to active.
        using var retry = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(retry);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());

        using var after = factory.Services.CreateScope();
        var pay = after.ServiceProvider.GetRequiredService<PayDbContext>();
        Assert.That(pay.Subscriptions.Single().Status, Is.EqualTo("active"), "the dead end is gone");
        Assert.That(pay.Checkouts.Single().Status, Is.EqualTo("paid"));
        Assert.That(pay.Charges.Count(), Is.EqualTo(1));
        Assert.That(pay.Subscriptions.Single().AttemptCount, Is.EqualTo(1), "dunning history is kept");
    }

    [Test]
    public async Task Failed_one_off_checkout_stays_terminal()
    {
        await using var factory = new PayApiFactory();
        factory.One.Responder = PayTest.Owner;
        var client = factory.CreateClient();
        var token = await CreateCheckout(client, "");
        await Fail(client, token);

        using var retry = new HttpRequestMessage(HttpMethod.Post, $"/v1/pay/{token}/start")
        {
            Content = new StringContent("""{"name":"Ada"}""", Encoding.UTF8, "application/json")
        };
        var response = await client.SendAsync(retry);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict),
            "one-off retries mint a fresh checkout; the failed one stays terminal");
    }
}
